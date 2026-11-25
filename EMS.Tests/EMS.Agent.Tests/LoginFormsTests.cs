using System.Runtime.Versioning;
using EMS.Agent.Login;
using EMS.Agent.Services;

namespace EMS.Agent.Tests;

[SupportedOSPlatform("windows")]
public class LoginFormsTests
{
    [Fact]
    public void SessionLoginForm_AsksForBothEmployeeCodeOrEmailAndPassword_WithoutSignedInAsBanner()
    {
        var loginService = new RecordingLoginService();
        using var form = new SessionLoginForm(loginService, "TL-LICENSE");

        var labels = form.Controls.OfType<Label>().Select(l => l.Text).ToList();
        var textBoxes = form.Controls.OfType<TextBox>().ToList();

        Assert.DoesNotContain(labels, text => text.Contains("Signed in as", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Employee code or email address", labels);
        Assert.Contains("Password", labels);
        Assert.Equal(2, textBoxes.Count);
        Assert.Contains(textBoxes, tb => tb.PasswordChar == '\0' && tb.Text == string.Empty);
        Assert.Contains(textBoxes, tb => tb.PasswordChar == '●' && tb.Text == string.Empty);
        Assert.True(form.TopMost);
    }

    [Fact]
    public async Task SessionLoginForm_SubmitWithEmailAndPassword_ForwardsEnteredCredentials()
    {
        var loginService = new RecordingLoginService();
        using var form = new SessionLoginForm(loginService, "TL-LICENSE");

        var textBoxes = form.Controls.OfType<TextBox>().ToList();
        var identifierBox = Assert.Single(textBoxes, tb => tb.PasswordChar == '\0');
        var passwordBox = Assert.Single(textBoxes, tb => tb.PasswordChar == '●');

        identifierBox.Text = "alice@company.com";
        passwordBox.Text = "Secret123!";

        await form.Card.SubmitAsync();

        Assert.NotNull(loginService.LastCall);
        Assert.Equal(("alice@company.com", "Secret123!", "TL-LICENSE"), loginService.LastCall!.Value);
    }

    [Fact]
    public async Task SessionLoginForm_SubmitWithMissingEmployeeCodeOrEmail_DoesNotCallService()
    {
        var loginService = new RecordingLoginService();
        using var form = new SessionLoginForm(loginService, "TL-LICENSE");

        var passwordBox = Assert.Single(form.Controls.OfType<TextBox>(), tb => tb.PasswordChar == '●');
        passwordBox.Text = "Secret123!";

        await form.Card.SubmitAsync();

        Assert.Null(loginService.LastCall);
    }

    [Fact]
    public void LoginForm_AsksForEmployeeCodeOrEmailAndPassword()
    {
        var loginService = new RecordingLoginService();
        using var form = new LoginForm(loginService, "TL-LICENSE", "Acme");

        var labels = form.Controls.OfType<Label>().Select(l => l.Text).ToList();
        var textBoxes = form.Controls.OfType<TextBox>().ToList();

        Assert.Contains("Employee code or email address", labels);
        Assert.Contains("Password", labels);
        Assert.Equal(2, textBoxes.Count);
        Assert.Contains(textBoxes, tb => tb.PasswordChar == '\0' && tb.Text == string.Empty);
        Assert.Contains(textBoxes, tb => tb.PasswordChar == '●' && tb.Text == string.Empty);
        Assert.False(form.TopMost);
    }

    [Fact]
    public void LoginWindowFactory_FreshDevice_ReturnsLicenseActivationForm()
    {
        var store = new FakeActivationStore(activated: false, licenseKey: null);
        using var form = LoginWindowFactory.Create(
            store, new StubLicenseService(), new RecordingLoginService(), bundledLicenseKey: "TL-BUNDLED");

        Assert.IsType<LicenseActivationForm>(form);
    }

    [Fact]
    public void LoginWindowFactory_EnrolledDeviceWithLicense_ReturnsSessionLoginForm()
    {
        var store = new FakeActivationStore(activated: true, licenseKey: "TL-SAVED");
        using var form = LoginWindowFactory.Create(
            store, new StubLicenseService(), new RecordingLoginService());

        Assert.IsType<SessionLoginForm>(form);
    }

    [Fact]
    public void LoginWindowFactory_EnrolledDeviceWithoutSavedLicense_UsesBundledLicenseWhenPresent()
    {
        var store = new FakeActivationStore(activated: true, licenseKey: null);
        using var form = LoginWindowFactory.Create(
            store, new StubLicenseService(), new RecordingLoginService(), bundledLicenseKey: "TL-BUNDLED");

        Assert.IsType<SessionLoginForm>(form);
    }

    [Fact]
    public void LoginWindowFactory_EnrolledDeviceWithoutAnyLicense_ReturnsLicenseActivationForm()
    {
        var store = new FakeActivationStore(activated: true, licenseKey: null);
        using var form = LoginWindowFactory.Create(
            store, new StubLicenseService(), new RecordingLoginService(), bundledLicenseKey: null);

        Assert.IsType<LicenseActivationForm>(form);
    }

    private sealed class RecordingLoginService : IActivationLoginService
    {
        public (string EmployeeCode, string Password, string LicenseKey)? LastCall { get; private set; }

        public Task<ActivationLoginResult> LoginAndActivateAsync(
            string employeeCode, string password, string licenseKey, CancellationToken cancellationToken = default)
        {
            LastCall = (employeeCode, password, licenseKey);
            return Task.FromResult(new ActivationLoginResult(true, "OK"));
        }
    }

    private sealed class StubLicenseService : ILicenseActivationService
    {
        public Task<LicenseValidationResult> ValidateAsync(
            string licenseKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new LicenseValidationResult(true, "OK", "1", "Acme"));
    }

    private sealed class FakeActivationStore : IActivationStore
    {
        private readonly bool _activated;
        private readonly string? _licenseKey;

        public FakeActivationStore(bool activated, string? licenseKey)
        {
            _activated = activated;
            _licenseKey = licenseKey;
        }

        public bool IsActivated() => _activated;
        public string? ActivatedBy() => _activated ? "EMP001" : null;
        public string? SessionBootId() => null;
        public string? LicenseKey() => _licenseKey;
        public void Activate(string activatedBy, string? licenseKey = null) { }
    }
}
