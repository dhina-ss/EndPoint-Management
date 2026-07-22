using EMS.Agent.Extensions;
using EMS.Agent.Helpers;
using EMS.Agent.Logging;
using EMS.Agent.Workers;

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
