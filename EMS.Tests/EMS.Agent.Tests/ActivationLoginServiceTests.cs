using System.Net;
using System.Runtime.Versioning;
using System.Text;
using EMS.Agent.Configuration;
using EMS.Agent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class ActivationLoginServiceTests
{
    private readonly RecordingActivationStore _store = new();

    private ActivationLoginService CreateService(HttpResponseMessage response)
        => CreateService(new StubHandler(response));

    private ActivationLoginService CreateService(StubHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:9999"),
        };
        var settings = Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:9999",
            LoginEndpoint = "/api/auth/login",
        });

        return new ActivationLoginService(
            httpClient, settings, _store, new FixedDeviceIdService(), NullLogger<ActivationLoginService>.Instance);
    }

    private sealed class FixedDeviceIdService : IDeviceIdService
    {
        public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("TEST-DEVICE-ID");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task LoginAndActivate_ValidCredentials_ActivatesDevice()
    {
        var service = CreateService(Json(HttpStatusCode.OK,
            """{"success":true,"message":"Login successful","username":"jane.doe","email":"j@x.com"}"""));

        var result = await service.LoginAndActivateAsync("jane.doe", "Secret123!", "TL-TEST");

        Assert.True(result.Success);
        Assert.True(_store.Activated);
        Assert.Equal("jane.doe", _store.ActivatedByUser);
    }

    [Fact]
    public async Task LoginAndActivate_WithEmailAddress_SendsEmailInEmployeeCodeAndActivates()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK,
            """{"success":true,"message":"Login successful","username":"jane.doe","email":"jane@company.com"}"""));
        var service = CreateService(handler);

        var result = await service.LoginAndActivateAsync("  jane@company.com ", "Secret123!", " TL-TEST ");

        Assert.True(result.Success);
        Assert.True(_store.Activated);
        Assert.Equal("jane@company.com", _store.ActivatedByUser);
        Assert.Equal("TL-TEST", _store.LicenseKeyValue);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"employeeCode\":\"jane@company.com\"", handler.LastRequestBody);
        Assert.Contains("\"licenseKey\":\"TL-TEST\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task LoginAndActivate_WrongPassword_DoesNotActivate()
    {
        var service = CreateService(Json(HttpStatusCode.OK,
            """{"success":false,"message":"Invalid username or password."}"""));

        var result = await service.LoginAndActivateAsync("jane.doe", "wrong", "TL-TEST");

        Assert.False(result.Success);
        Assert.Equal("Invalid username or password.", result.Message);
        Assert.False(_store.Activated);
    }

    [Fact]
    public async Task LoginAndActivate_UnauthorizedResponse_ReturnsFriendlyMessage()
    {
        var service = CreateService(Json(HttpStatusCode.Unauthorized,
            """{"success":false,"message":"Invalid employee code or password."}"""));

        var result = await service.LoginAndActivateAsync("jane@company.com", "wrong", "TL-TEST");

        Assert.False(result.Success);
        Assert.Equal("Invalid employee code or password.", result.Message);
        Assert.False(_store.Activated);
    }

    [Fact]
    public async Task LoginAndActivate_EmptyInput_DoesNotCallServerOrActivate()
    {
        var service = CreateService(Json(HttpStatusCode.OK, """{"success":true}"""));

        var result = await service.LoginAndActivateAsync("", "", "TL-TEST");

        Assert.False(result.Success);
        Assert.False(_store.Activated);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public string? LastRequestBody { get; private set; }

        public StubHandler(HttpResponseMessage response) => _response = response;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return _response;
        }
    }

    private sealed class RecordingActivationStore : IActivationStore
    {
        public bool Activated { get; private set; }
        public string? ActivatedByUser { get; private set; }

        public bool IsActivated() => Activated;
        public string? ActivatedBy() => ActivatedByUser;
        public string? SessionBootId() => null;
        public string? LicenseKey() => LicenseKeyValue;
        public string? LicenseKeyValue { get; private set; }

        public void Activate(string activatedBy, string? licenseKey = null)
        {
            Activated = true;
            ActivatedByUser = activatedBy;
            LicenseKeyValue = licenseKey;
        }
    }
}
