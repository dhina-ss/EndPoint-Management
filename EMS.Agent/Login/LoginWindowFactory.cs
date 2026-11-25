using System.Runtime.Versioning;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// Selects the initial window for <c>--login</c>: if the device is already
/// enrolled with a tenant license key, opens <see cref="SessionLoginForm"/>
/// (which asks for employee code or email address and password); otherwise
/// starts at <see cref="LicenseActivationForm"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class LoginWindowFactory
{
    public static Form Create(
        IActivationStore activationStore,
        ILicenseActivationService licenseService,
        IActivationLoginService loginService,
        string? bundledLicenseKey = null)
    {
        var licenseKey = activationStore.LicenseKey();
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            licenseKey = bundledLicenseKey;
        }

        if (activationStore.IsActivated() && !string.IsNullOrWhiteSpace(licenseKey))
        {
            return new SessionLoginForm(loginService, licenseKey);
        }

        return new LicenseActivationForm(licenseService, loginService);
    }
}
