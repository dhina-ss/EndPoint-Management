using System.Runtime.Versioning;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// Sign-in window shown when the device is already enrolled (such as during
/// per-startup re-authentication or re-opening the sign-in prompt). Always asks
/// for both employee code or email address and password scoped to the
/// remembered tenant license; a correct sign-in re-records this boot session,
/// which releases the internet block.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SessionLoginForm : Form
{
    private readonly EmployeeSignInCard _card;

    public SessionLoginForm(IActivationLoginService loginService, string licenseKey)
    {
        // Closable: the firewall (not this window) enforces the block, and the
        // tray re-opens this if it's dismissed without signing in - so a brief
        // server hiccup can never strand the user behind an unclosable window.
        _card = new EmployeeSignInCard(
            this,
            loginService,
            licenseKey,
            title: "The Lance Endpoint — Sign in",
            headingText: "Sign in to continue",
            subheadingText: "Enter your employee code or email address and password.",
            submitButtonText: "Sign in",
            topMost: true,
            onSuccess: (_, _) => Close());
    }

    internal EmployeeSignInCard Card => _card;
}
