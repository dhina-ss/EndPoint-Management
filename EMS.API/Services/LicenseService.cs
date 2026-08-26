using System.Data;
using System.Globalization;
using EMS.API.Data;
using EMS.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Services;

public class LicenseService : ILicenseService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(ApplicationDbContext dbContext, ILogger<LicenseService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<LicenseValidateResponse> ValidateAsync(
        string licenseKey, CancellationToken cancellationToken = default)
    {
        var key = licenseKey?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new LicenseValidateResponse { Valid = false, Message = "Enter your license key." };
        }

        // Raw SQL over the shared connection: the tenants table is created and
        // owned by the portal, so the API reads it without an EF entity.
        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, tenant_name, status, expiry_date FROM tenants WHERE license_key = @key LIMIT 1;";
        var p = cmd.CreateParameter();
        p.ParameterName = "key";
        p.Value = key;
        cmd.Parameters.Add(p);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new LicenseValidateResponse { Valid = false, Message = "Invalid license key." };
            }

            var tenantId = reader["id"]?.ToString();
            var tenantName = reader["tenant_name"] as string;
            var status = reader["status"] as string;
            var expiryRaw = reader["expiry_date"] as string;

            if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return new LicenseValidateResponse
                {
                    Valid = false,
                    Message = $"This license is {status?.ToLowerInvariant() ?? "inactive"}. Contact your administrator.",
                    TenantId = tenantId,
                    TenantName = tenantName
                };
            }

            if (IsExpired(expiryRaw, out var expiryDate))
            {
                return new LicenseValidateResponse
                {
                    Valid = false,
                    Message = $"This license expired on {expiryDate:yyyy-MM-dd}. Contact your administrator.",
                    TenantId = tenantId,
                    TenantName = tenantName
                };
            }

            return new LicenseValidateResponse
            {
                Valid = true,
                Message = "License verified.",
                TenantId = tenantId,
                TenantName = tenantName
            };
        }
        catch (Exception ex)
        {
            // Most likely the license_key column doesn't exist yet (portal
            // migration not applied). Fail closed with a clear log.
            _logger.LogError(ex, "License validation query failed for key {Key}.", key);
            return new LicenseValidateResponse { Valid = false, Message = "License validation is unavailable. Try again later." };
        }
    }

    /// <summary>
    /// expiry_date is stored as free text by the portal; treat it as expired
    /// only when it parses to a date strictly before today. Unparseable or
    /// empty values are treated as non-expiring.
    /// </summary>
    private static bool IsExpired(string? raw, out DateTime expiryDate)
    {
        expiryDate = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string[] formats = { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "dd/MM/yyyy" };
        if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out expiryDate)
            || DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out expiryDate))
        {
            return expiryDate.Date < DateTime.UtcNow.Date;
        }

        return false;
    }
}
