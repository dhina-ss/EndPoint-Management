using System.Net;
using System.Runtime.Versioning;
using System.Text;
using EMS.Agent.Configuration;
using EMS.Agent.Models;
using EMS.Agent.Services;
using EMS.Shared.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class ApiClientServiceTests
{
    private const string TestDeviceId = "11111111-2222-3333-4444-555555555555";

    private readonly InMemoryTokenService _tokenService = new();

    private ApiClientService CreateService(SequenceHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:9999")
        };

        var settings = Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:9999",
            RegisterEndpoint = "/api/devices/register",
            HeartbeatEndpoint = "/api/devices/heartbeat",
            MaxRetryAttempts = 3,
            RetryDelaySeconds = 0
        });

        return new ApiClientService(
            httpClient,
            settings,
            _tokenService,
            new FixedDeviceIdService(TestDeviceId),
            NullLogger<ApiClientService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage RegistrationSuccess(string token = "issued-token")
        => JsonResponse(HttpStatusCode.OK,
            $$"""{"success":true,"message":"Device registered successfully","deviceId":"abc","token":"{{token}}"}""");

    private static HttpResponseMessage HeartbeatSuccess()
        => JsonResponse(HttpStatusCode.OK,
            """{"success":true,"message":"Heartbeat received","serverTime":"2026-07-19T10:30:00Z"}""");

    [Fact]
    public async Task RegisterDeviceAsync_Success_ReturnsTrueAndStoresToken()
    {
        var handler = new SequenceHandler(RegistrationSuccess("token-abc"));
        var result = await CreateService(handler).RegisterDeviceAsync(new DeviceInventoryModel());

        Assert.True(result);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("token-abc", await _tokenService.GetTokenAsync());
    }

    [Fact]
    public async Task RegisterDeviceAsync_FirstContact_SendsNoCredentialHeaders()
    {
        var handler = new SequenceHandler(RegistrationSuccess());

        await CreateService(handler).RegisterDeviceAsync(new DeviceInventoryModel { DeviceId = TestDeviceId });

        var request = Assert.Single(handler.Requests);
        Assert.False(request.Headers.Contains(DeviceAuthHeaders.Token));
    }

    [Fact]
    public async Task RegisterDeviceAsync_WithStoredToken_SendsCredentialHeaders()
    {
        await _tokenService.SaveTokenAsync(TestDeviceId, "existing-token");
        var handler = new SequenceHandler(RegistrationSuccess());

        await CreateService(handler).RegisterDeviceAsync(new DeviceInventoryModel { DeviceId = TestDeviceId });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(TestDeviceId, request.Headers.GetValues(DeviceAuthHeaders.DeviceId).Single());
        Assert.Equal("existing-token", request.Headers.GetValues(DeviceAuthHeaders.Token).Single());
    }

    [Fact]
    public async Task RegisterDeviceAsync_TransientServerError_RetriesAndSucceeds()
    {
        var handler = new SequenceHandler(
            JsonResponse(HttpStatusCode.InternalServerError, """{"success":false,"message":"boom"}"""),
            RegistrationSuccess());

        var result = await CreateService(handler).RegisterDeviceAsync(new DeviceInventoryModel());

        Assert.True(result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task RegisterDeviceAsync_ValidationError_FailsWithoutRetry()
    {
        var handler = new SequenceHandler(
            JsonResponse(HttpStatusCode.BadRequest, """{"title":"One or more validation errors occurred."}"""));

        var result = await CreateService(handler).RegisterDeviceAsync(new DeviceInventoryModel());

        Assert.False(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RegisterDeviceAsync_ServerUnreachable_FailsAfterAllRetries()
    {
        var handler = new SequenceHandler();
        var result = await CreateService(handler).RegisterDeviceAsync(new DeviceInventoryModel());

        Assert.False(result);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithoutToken_SkipsHttpCall()
    {
        var handler = new SequenceHandler(HeartbeatSuccess());
        var result = await CreateService(handler).SendHeartbeatAsync(new HeartbeatModel());

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithToken_SendsCredentialHeaders()
    {
        await _tokenService.SaveTokenAsync(TestDeviceId, "token-xyz");
        var handler = new SequenceHandler(HeartbeatSuccess());

        var result = await CreateService(handler).SendHeartbeatAsync(new HeartbeatModel());

        Assert.True(result.Success);
        Assert.Equal(1, handler.CallCount);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(TestDeviceId, request.Headers.GetValues(DeviceAuthHeaders.DeviceId).Single());
        Assert.Equal("token-xyz", request.Headers.GetValues(DeviceAuthHeaders.Token).Single());
    }

    [Fact]
    public async Task SendHeartbeatAsync_Unauthorized_ReturnsFalse()
    {
        await _tokenService.SaveTokenAsync(TestDeviceId, "stale-token");
        var handler = new SequenceHandler(
            JsonResponse(HttpStatusCode.Unauthorized, """{"success":false,"message":"Invalid device credentials"}"""));

        var result = await CreateService(handler).SendHeartbeatAsync(new HeartbeatModel());

        Assert.False(result.Success);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendHeartbeatAsync_ReturnsUsbBlockingStateFromServer()
    {
        await _tokenService.SaveTokenAsync(TestDeviceId, "token-abc");
        var handler = new SequenceHandler(JsonResponse(HttpStatusCode.OK,
            """{"success":true,"message":"Heartbeat received","serverTime":"2026-07-19T10:30:00Z","usbBlockingEnabled":true}"""));

        var result = await CreateService(handler).SendHeartbeatAsync(new HeartbeatModel());

        Assert.True(result.Success);
        Assert.True(result.UsbBlockingEnabled);
    }

    private sealed class InMemoryTokenService : IDeviceTokenService
    {
        private string? _token;

        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_token);

        public Task SaveTokenAsync(string deviceId, string token, CancellationToken cancellationToken = default)
        {
            _token = token;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedDeviceIdService : IDeviceIdService
    {
        private readonly string _deviceId;

        public FixedDeviceIdService(string deviceId) => _deviceId = deviceId;

        public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_deviceId);
    }

    /// <summary>
    /// Returns queued responses in order and records every request; throws
    /// HttpRequestException (connection failure) once the queue is empty.
    /// </summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public int CallCount { get; private set; }

        public List<HttpRequestMessage> Requests { get; } = new();

        public SequenceHandler(params HttpResponseMessage[] responses)
            => _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Requests.Add(request);

            return _responses.Count > 0
                ? Task.FromResult(_responses.Dequeue())
                : Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
        }
    }
}
