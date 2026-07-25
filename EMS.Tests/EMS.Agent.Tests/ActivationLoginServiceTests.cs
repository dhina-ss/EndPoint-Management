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
    {
        var httpClient = new HttpClient(new StubHandler(response))
        {
            BaseAddress = new Uri("https://localhost:9999"),
        };
        var settings = Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:9999",
            LoginEndpoint = "/api/auth/login",
        });

        return new ActivationLoginService(
            httpClient, settings, _store, NullLogger<ActivationLoginService>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task LoginAndActivate_ValidCredentials_ActivatesDevice()
    {
        var service = CreateService(Json(HttpStatusCode.OK,
            """{"success":true,"message":"Login successful","username":"jane.doe","email":"j@x.com"}"""));

        var result = await service.LoginAndActivateAsync("jane.doe", "Secret123!");

        Assert.True(result.Success);
        Assert.True(_store.Activated);
        Assert.Equal("jane.doe", _store.ActivatedByUser);
    }

    [Fact]
    public async Task LoginAndActivate_WrongPassword_DoesNotActivate()
    {
        var service = CreateService(Json(HttpStatusCode.OK,
            """{"success":false,"message":"Invalid username or password."}"""));

        var result = await service.LoginAndActivateAsync("jane.doe", "wrong");

        Assert.False(result.Success);
        Assert.Equal("Invalid username or password.", result.Message);
        Assert.False(_store.Activated);
    }

    [Fact]
    public async Task LoginAndActivate_EmptyInput_DoesNotCallServerOrActivate()
    {
        var service = CreateService(Json(HttpStatusCode.OK, """{"success":true}"""));

        var result = await service.LoginAndActivateAsync("", "");

        Assert.False(result.Success);
        Assert.False(_store.Activated);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public StubHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    private sealed class RecordingActivationStore : IActivationStore
    {
        public bool Activated { get; private set; }
        public string? ActivatedByUser { get; private set; }

        public bool IsActivated() => Activated;
        public string? ActivatedBy() => ActivatedByUser;

        public void Activate(string activatedBy)
        {
            Activated = true;
            ActivatedByUser = activatedBy;
        }
    }
}
