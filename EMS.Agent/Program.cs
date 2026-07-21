using EMS.Agent.Extensions;
using EMS.Agent.Workers;

// Foreground-window tracking cannot run inside the Windows Service: services
// run in Session 0, which has no desktop and cannot see the interactive
// user's foreground window (that isolation is deliberate, since Windows
// Vista). This mode instead runs as a per-user process, launched by a
// Scheduled Task with a logon trigger (see installer/EMS.Agent.iss), so it
// executes inside the same session as the logged-in user.
if (args.Contains("--usage-tracker"))
{
    var trackerBuilder = Host.CreateApplicationBuilder(args);
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
