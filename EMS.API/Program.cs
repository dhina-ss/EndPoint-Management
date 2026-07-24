using EMS.API.Configuration;
using EMS.API.Data;
using EMS.API.Middleware;
using EMS.API.Repositories;
using EMS.API.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// Cloud platforms such as Render assign a dynamic port via the PORT
// environment variable and route to it on 0.0.0.0; Kestrel does not read
// PORT on its own; without this the app binds to localhost:5000 and the
// platform's proxy can never reach it (TLS succeeds, then the request hangs).
var cloudPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(cloudPort) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{cloudPort}");
}

// Configuration
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection(DatabaseSettings.SectionName));

var databaseSettings = builder.Configuration
    .GetSection(DatabaseSettings.SectionName)
    .Get<DatabaseSettings>() ?? new DatabaseSettings();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// Entity Framework Core + PostgreSQL (local or Neon; Neon requires
// "SSL Mode=Require" in the connection string, which Npgsql handles natively).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(
            maxRetryCount: databaseSettings.MaxRetryCount,
            maxRetryDelay: TimeSpan.FromSeconds(databaseSettings.MaxRetryDelaySeconds),
            errorCodesToAdd: null);
        npgsql.CommandTimeout(databaseSettings.CommandTimeoutSeconds);
    });

    if (databaseSettings.EnableSensitiveDataLogging && builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

// Application services
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IDeviceAuthRepository, DeviceAuthRepository>();
builder.Services.AddScoped<IHeartbeatRepository, HeartbeatRepository>();
builder.Services.AddScoped<IAppUsageRepository, AppUsageRepository>();
builder.Services.AddScoped<IBlockedWebsiteRepository, BlockedWebsiteRepository>();
builder.Services.AddScoped<IApplicationInventoryRepository, ApplicationInventoryRepository>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IHeartbeatService, HeartbeatService>();
builder.Services.AddScoped<IAppUsageService, AppUsageService>();
builder.Services.AddScoped<IBlockedWebsiteService, BlockedWebsiteService>();
builder.Services.AddScoped<IApplicationInventoryService, ApplicationInventoryService>();
builder.Services.AddScoped<IDeviceAuthService, DeviceAuthService>();
builder.Services.AddScoped<ITokenValidationService, TokenValidationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cloud platforms terminate TLS at their proxy; honor the forwarded scheme
// and client IP so HTTPS redirection and logging see the real values.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS for the browser-based dashboard (the agent is unaffected — it is not
// a browser). Origins come from configuration, e.g. Cors__AllowedOrigins__0.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Liveness/readiness probe: /health is Healthy only when the database answers.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

// Verify database connectivity before serving traffic: a clear startup
// failure beats a stream of 500s. Uses the configured retry policy.
using (var startupScope = app.Services.CreateScope())
{
    var dbContext = startupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var dbConnection = dbContext.Database.GetDbConnection();

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            throw new InvalidOperationException("The database did not accept a connection.");
        }

        startupLogger.LogInformation(
            "Database connection verified: {Database} on {Host}.",
            dbConnection.Database, dbConnection.DataSource);

        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Count > 0)
        {
            startupLogger.LogWarning(
                "The database is missing {Count} migration(s): {Migrations}. Run 'dotnet ef database update --project EMS.API'.",
                pendingMigrations.Count, string.Join(", ", pendingMigrations));
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex,
            "Cannot connect to database {Database} on {Host}. " +
            "Check ConnectionStrings:DefaultConnection (for Neon it must include 'SSL Mode=Require'). The API will not start.",
            dbConnection.Database, dbConnection.DataSource);
        return 1;
    }
}

app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors(FrontendCorsPolicy);
app.UseMiddleware<DeviceAuthenticationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In Development the agent talks plain HTTP; redirecting would bounce it to
// the HTTPS endpoint, which fails until the dev certificate is trusted.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

// Anonymous by design: platform load balancers probe it without credentials.
app.MapHealthChecks("/health");

app.Run();

return 0;
