using EMS.Agent.Extensions;
using EMS.Agent.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Runs as a Windows Service when installed via sc.exe; runs as a console app otherwise.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "EMS Endpoint Agent";
});

builder.Services.AddAgentServices(builder.Configuration);
builder.Services.AddHostedService<AgentWorker>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<AppUsageWorker>();

var host = builder.Build();
host.Run();
