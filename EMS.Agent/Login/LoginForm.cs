using System.Runtime.Versioning;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// Activation login window shown after install. Collects EMS credentials
/// (employee code or email address and password), verifies them against the
/// server, and on success activates the device so the background service begins
/// working. The window keeps no state beyond the fields; all logic lives in
/// <see cref="IActivationLoginService"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LoginForm : Form
{
    private readonly EmployeeSignInCard _card;

    public LoginForm(IActivationLoginService loginService, string licenseKey, string? tenantName = null)
    {
        var subheading = string.IsNullOrWhiteSpace(tenantName)
            ? "Use your employee code or email address and password."
            : $"Tenant: {tenantName}. Use your employee code or email address.";

        _card = new EmployeeSignInCard(
            this,
            loginService,
            licenseKey,
            title: "The Lance Endpoint — Activation",
            headingText: "Sign in to activate",
            subheadingText: subheading,
            submitButtonText: "Activate",
            topMost: false,
            onSuccess: (card, result) =>
            {
                card.SetStatus(result.Message, Color.Green);
                MessageBox.Show(
                    result.Message + "\n\nThe Lance Endpoint is now active on this device.",
                    "The Lance Endpoint — Activated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Close();
            });
    }

    internal EmployeeSignInCard Card => _card;
}
