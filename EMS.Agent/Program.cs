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
    loginBuilder.Services.AddHttpClient<IActivationLoginService, ActivationLoginService>((sp, client) =>
    {
        var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EMS.Agent.Configuration.ApiSettings>>().Value;
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
        }
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    });

    using var loginHost = loginBuilder.Build();
    var loginService = loginHost.Services.GetRequiredService<IActivationLoginService>();

    ApplicationConfiguration.Initialize();
    System.Windows.Forms.Application.Run(new LoginForm(loginService));
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

var host = builder.Build();
host.Run();
