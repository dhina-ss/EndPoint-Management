using EMS.Agent.Configuration;
using EMS.Agent.Services;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Extensions;

/// <summary>
/// Dependency injection registrations for the agent.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiSettings>(configuration.GetSection(ApiSettings.SectionName));

        // Singleton so the DeviceId is resolved from disk once per process.
        services.AddSingleton<IDeviceIdService, DeviceIdService>();

        // Activation gate: the service reads it; the login window writes it.
        services.AddSingleton<IActivationStore, ActivationStore>();

        // Microsoft Store unlock state: the service reads it; the unlock
        // window writes it after verifying the admin password.
        services.AddSingleton<IStoreUnlockStore, StoreUnlockStore>();

        // Singleton: caches the persisted token; the latest registration's
        // token must be visible to the heartbeat worker across scopes.
        services.AddSingleton<IDeviceTokenService, DeviceTokenService>();

        // Singleton: the accumulator must survive across every sample tick
        // until the worker flushes and uploads it.
        services.AddSingleton<IAppUsageTrackerService, AppUsageTrackerService>();

        // Session lock/suspend listener and the work-time accumulator. Both are
        // singletons (state must survive across ticks); constructed only in the
        // per-user tracker process that resolves the AppUsageWorker.
        services.AddSingleton<ISessionStateService, SessionStateService>();
        services.AddSingleton<IWorkTimeTracker, WorkTimeTracker>();

        // Singleton: network throughput is a rate, so the previous sample's
        // byte counters must survive between heartbeats.
        services.AddSingleton<ISystemMetricsService, SystemMetricsService>();

        services.AddScoped<IDeviceCollectorService, DeviceCollectorService>();
        services.AddScoped<IHeartbeatService, HeartbeatService>();

        // Executes software-management commands (uninstall/install/update).
        services.AddScoped<ICommandExecutionService, CommandExecutionService>();

        services.AddHttpClient<IApiClientService, ApiClientService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ApiSettings>>().Value;

            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                client.BaseAddress = new Uri(settings.BaseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        return services;
    }
}
