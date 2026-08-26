using EMS.Agent.Extensions;
using EMS.Agent.Helpers;
using EMS.Agent.Login;
using EMS.Agent.Logging;
using EMS.Agent.Services;
using EMS.Agent.Workers;

// Activation login window, shown after install (launched by the installer)
// and re-openable from the Start Menu. Runs interactively in the user's
// session, verifies EMS credentials, and on success writes the activation
// gate that the background service waits on.
if (args.Contains("--login"))
{
    ConsoleWindowHelper.DetachConsole();

    var loginBuilder = Host.CreateApplicationBuilder(args);
    loginBuilder.Services.AddAgentServices(loginBuilder.Configuration);

    void ConfigureApiClient(IServiceProvider sp, HttpClient client)
    {
        var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EMS.Agent.Configuration.ApiSettings>>().Value;
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
        }
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    }

    // UseProxy=false so the sign-in call bypasses the block-page system proxy
    // (which is active precisely while this window is up) and reaches the server.
    static System.Net.Http.HttpClientHandler DirectHandler() =>
        new System.Net.Http.HttpClientHandler { UseProxy = false };

    loginBuilder.Services.AddHttpClient<IActivationLoginService, ActivationLoginService>(ConfigureApiClient)
        .ConfigurePrimaryHttpMessageHandler(DirectHandler);
    loginBuilder.Services.AddHttpClient<ILicenseActivationService, LicenseActivationService>(ConfigureApiClient)
        .ConfigurePrimaryHttpMessageHandler(DirectHandler);

    using var loginHost = loginBuilder.Build();
    var loginService = loginHost.Services.GetRequiredService<IActivationLoginService>();
    var licenseService = loginHost.Services.GetRequiredService<ILicenseActivationService>();

    ApplicationConfiguration.Initialize();
    // Two-step activation: license key first, then employee sign-in.
    System.Windows.Forms.Application.Run(new LicenseActivationForm(licenseService, loginService));
    return;
}

// "Unlock Microsoft Store" window, launched from the Start Menu. Runs in the
// user's session, verifies an EMS admin password, and grants a temporary
// unlock that the SYSTEM service acts on at its next heartbeat.
if (args.Contains("--unlock-store"))
{
    ConsoleWindowHelper.DetachConsole();

    var unlockBuilder = Host.CreateApplicationBuilder(args);
    unlockBuilder.Services.AddAgentServices(unlockBuilder.Configuration);
    unlockBuilder.Services.AddHttpClient<IStoreUnlockService, StoreUnlockService>((sp, client) =>
    {
        var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EMS.Agent.Configuration.ApiSettings>>().Value;
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
        }
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    });

    using var unlockHost = unlockBuilder.Build();
    var unlockService = unlockHost.Services.GetRequiredService<IStoreUnlockService>();

    ApplicationConfiguration.Initialize();
    System.Windows.Forms.Application.Run(new StoreUnlockForm(unlockService));
    return;
}

// "Authorize Uninstall" window, launched by the installer's uninstaller. Shows a
// prompt for EMS admin credentials, verifies them against the server, and exits
// 0 only when authorized - the uninstaller aborts on any non-zero exit.
if (args.Contains("--verify-admin"))
{
    ConsoleWindowHelper.DetachConsole();

    var verifyBuilder = Host.CreateApplicationBuilder(args);
    verifyBuilder.Services.AddAgentServices(verifyBuilder.Configuration);
    verifyBuilder.Services.AddHttpClient<IAdminAuthService, AdminAuthService>((sp, client) =>
    {
        var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EMS.Agent.Configuration.ApiSettings>>().Value;
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
        }
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    });

    using var verifyHost = verifyBuilder.Build();
    var authService = verifyHost.Services.GetRequiredService<IAdminAuthService>();

    ApplicationConfiguration.Initialize();
    var authForm = new UninstallAuthForm(authService);
    System.Windows.Forms.Application.Run(authForm);

    Environment.Exit(authForm.Authorized ? 0 : 1);
}

// Uninstall notification: the uninstaller runs this (after admin authorization,
// before local state is wiped) so the server removes this device's record.
if (args.Contains("--report-uninstall"))
{
    ConsoleWindowHelper.DetachConsole();

    var reportBuilder = Host.CreateApplicationBuilder(args);
    reportBuilder.Services.AddAgentServices(reportBuilder.Configuration);

    using var reportHost = reportBuilder.Build();
    var apiClient = reportHost.Services.GetRequiredService<IApiClientService>();
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        apiClient.ReportUninstallAsync(cts.Token).GetAwaiter().GetResult();
    }
    catch
    {
        // Best-effort: never block the uninstall on a server round-trip.
    }

    // Remove the block-page root certificate we installed, so uninstalling the
    // agent fully removes its trust footprint.
    if (OperatingSystem.IsWindows())
    {
        EMS.Agent.Services.BlockCertificateService.RemoveTrustedRoot();
    }
    return;
}

// Foreground-window tracking cannot run inside the Windows Service: services
// run in Session 0, which has no desktop and cannot see the interactive
// user's foreground window (that isolation is deliberate, since Windows
// Vista). This mode instead runs as a per-user process, launched by a
// Scheduled Task with a logon trigger (see installer/EMS.Agent.iss), so it
// executes inside the same session as the logged-in user.
if (args.Contains("--usage-tracker"))
{
    // The Worker SDK builds a console-subsystem exe; unlike the Service
    // Control Manager, Task Scheduler leaves that console window visible on
    // the user's desktop. This mode logs to a file (below), not the
    // console, so detach it immediately - nothing is lost.
    ConsoleWindowHelper.DetachConsole();

    var trackerBuilder = Host.CreateApplicationBuilder(args);

    // Writing to the Windows Event Log from this non-elevated, per-user
    // process has proven unreliable (silent write failures depending on the
    // machine's Event Log ACLs), so this mode also logs to a plain file
    // that does not depend on those permissions.
    var logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EMS.Agent", "usage-tracker.log");
    trackerBuilder.Logging.AddProvider(new FileLoggerProvider(logFilePath));

    trackerBuilder.Services.AddAgentServices(trackerBuilder.Configuration);
    trackerBuilder.Services.AddHostedService<AppUsageWorker>();
    // Per-user tray icon: green check when activated, red cross when not.
    trackerBuilder.Services.AddHostedService<TrayIconWorker>();
    // Optional precise GPS location (falls back to the server's IP location).
    trackerBuilder.Services.AddHostedService<LocationWorker>();
    // Branded "internet blocked - please sign in" page + system proxy, shown
    // while the require-login policy holds this session.
    trackerBuilder.Services.AddHostedService<BlockPageWorker>();
    trackerBuilder.Build().Run();
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// Runs as a Windows Service when installed via sc.exe; runs as a console app otherwise.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "EMS Endpoint Agent";
});

builder.Services.AddAgentServices(builder.Configuration);
builder.Services.AddHostedService<AgentWorker>();
builder.Services.AddHostedService<HeartbeatWorker>();
// Software-management command queue runs only in the SYSTEM service, which has
// the rights to install/uninstall machine-wide software.
builder.Services.AddHostedService<CommandWorker>();
// Enforces "require sign-in on every startup" by blocking internet (firewall)
// until the user signs in; needs SYSTEM rights, so it lives in the service.
builder.Services.AddHostedService<LoginEnforcementWorker>();

// Serves the branded "site blocked" page (HTTP + HTTPS) for domains the hosts
// file redirects to loopback; installs the local trusted root it signs with.
// Needs SYSTEM rights to bind :80/:443 and write the machine trust store.
builder.Services.AddHostedService<WebsiteBlockPageWorker>();

// Learns the tenant's require-login policy from the bundled license before the
// first activation, so a fresh device enforces it from boot one.
builder.Services.AddHttpClient<ILicenseActivationService, LicenseActivationService>((sp, client) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EMS.Agent.Configuration.ApiSettings>>().Value;
    if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
    {
        client.BaseAddress = new Uri(settings.BaseUrl);
    }
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
}).ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { UseProxy = false });
builder.Services.AddHostedService<LicensePolicySyncWorker>();

var host = builder.Build();
host.Run();
