using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EMS.Agent.Services;

/// <summary>
/// Manages the local "The Lance Endpoint" root certificate authority used to
/// serve the branded HTTPS block page. A single CA is generated per device
/// (its private key never leaves the machine) and installed into the machine's
/// Trusted Root store, so browsers accept the leaf certificates this service
/// mints on the fly for each blocked host - including HSTS/pinned sites, since
/// Chrome/Edge/Firefox deliberately skip key-pinning for locally-installed
/// roots. The CA only ever signs leaves that serve a static block page; no real
/// traffic is intercepted (only domains the hosts file points at 127.0.0.1
/// reach us).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BlockCertificateService
{
    private const string CaSubject = "CN=The Lance Endpoint Root, O=The Lance";
    private const string CaFriendlyName = "The Lance Endpoint Root";

    private static string CaFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EMS.Agent", "block-ca.pfx");

    private readonly ILogger _logger;
    private readonly object _sync = new();
    private X509Certificate2? _ca;
    private readonly ConcurrentDictionary<string, X509Certificate2> _leafCache = new(StringComparer.OrdinalIgnoreCase);

    public BlockCertificateService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads (or creates) the CA and ensures it is trusted by the machine.
    /// Returns false if the CA could not be established, in which case HTTPS
    /// block pages are unavailable (the site is still blocked, just unbranded).
    /// </summary>
    public bool EnsureReady()
    {
        lock (_sync)
        {
            try
            {
                _ca ??= LoadOrCreateCa();
                EnsureTrusted(_ca);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not establish the block-page certificate authority.");
                return false;
            }
        }
    }

    /// <summary>Mints (and caches) a leaf certificate for the given host, signed by the CA.</summary>
    public X509Certificate2? GetLeafForHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            host = "blocked.local";
        }
        host = host.Trim().ToLowerInvariant();

        if (_leafCache.TryGetValue(host, out var cached))
        {
            return cached;
        }

        lock (_sync)
        {
            if (_leafCache.TryGetValue(host, out cached))
            {
                return cached;
            }
            if (_ca is null && !EnsureReady())
            {
                return null;
            }

            try
            {
                var leaf = CreateLeaf(_ca!, host);
                _leafCache[host] = leaf;
                return leaf;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not mint a block-page certificate for {Host}.", host);
                return null;
            }
        }
    }

    private X509Certificate2 LoadOrCreateCa()
    {
        var path = CaFilePath;
        if (File.Exists(path))
        {
            try
            {
                var loaded = new X509Certificate2(
                    File.ReadAllBytes(path), (string?)null,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
                if (loaded.NotAfter > DateTime.Now.AddDays(30) && loaded.HasPrivateKey)
                {
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Existing block CA could not be loaded; regenerating.");
            }
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(CaSubject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, critical: true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, critical: false));

        var ca = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));
        ca.FriendlyName = CaFriendlyName;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, ca.Export(X509ContentType.Pkcs12));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not persist the block CA; a new one will be made next run.");
        }

        return ca;
    }

    private static void EnsureTrusted(X509Certificate2 ca)
    {
        // Public cert only (no private key) into the machine Trusted Root store.
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        var already = store.Certificates.Find(X509FindType.FindByThumbprint, ca.Thumbprint, validOnly: false);
        if (already.Count == 0)
        {
            using var publicOnly = new X509Certificate2(ca.Export(X509ContentType.Cert));
            publicOnly.FriendlyName = CaFriendlyName;
            store.Add(publicOnly);
        }
        store.Close();
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 ca, string host)
    {
        using var leafRsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={host}", leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: false));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") /* server auth */ }, critical: false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(host);
        if (!host.StartsWith("www.", StringComparison.Ordinal))
        {
            san.AddDnsName("www." + host);
        }
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        var serial = new byte[16];
        RandomNumberGenerator.Fill(serial);
        using var leaf = req.Create(ca, DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(2), serial);
        // Bind the private key and round-trip through PFX so SslStream can use it.
        using var withKey = leaf.CopyWithPrivateKey(leafRsa);
        return new X509Certificate2(withKey.Export(X509ContentType.Pkcs12));
    }

    /// <summary>Removes the trusted root (used at uninstall). Best-effort, static so the uninstaller path can call it.</summary>
    public static void RemoveTrustedRoot()
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            var found = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, CaSubject, validOnly: false);
            foreach (var cert in found)
            {
                store.Remove(cert);
            }
            store.Close();
        }
        catch
        {
            // Best-effort.
        }
    }
}
