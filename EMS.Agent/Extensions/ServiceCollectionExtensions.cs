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

        // Singleton: caches the persisted token; the latest registration's
        // token must be visible to the heartbeat worker across scopes.
        services.AddSingleton<IDeviceTokenService, DeviceTokenService>();

        // Singleton: the accumulator must survive across every sample tick
        // until the worker flushes and uploads it.
        services.AddSingleton<IAppUsageTrackerService, AppUsageTrackerService>();

        services.AddScoped<IDeviceCollectorService, DeviceCollectorService>();
        services.AddScoped<IHeartbeatService, HeartbeatService>();

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
