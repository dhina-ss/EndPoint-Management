using System.Runtime.Versioning;
using EMS.Agent.Branding;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// Per-startup re-authentication window. Shown when the device is already
/// enrolled and the "require sign-in every startup" policy holds this power-on
/// session. It asks only for the password (the employee code and tenant license
/// are already remembered from the first activation); a correct password
/// re-records this boot session, which releases the internet block.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SessionLoginForm : Form
{
    private readonly IActivationLoginService _loginService;
    private readonly string _employeeCode;
    private readonly string _licenseKey;

    private readonly TextBox _passwordBox;
    private readonly Label _statusLabel;
    private readonly Button _signInButton;

    public SessionLoginForm(IActivationLoginService loginService, string employeeCode, string licenseKey)
    {
        _loginService = loginService;
        _employeeCode = employeeCode;
        _licenseKey = licenseKey;

        Text = "The Lance Endpoint — Sign in";
        try { Icon = BrandingAssets.AppIcon(); } catch { /* non-fatal */ }
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        // Closable: the firewall (not this window) enforces the block, and the
        // tray re-opens this if it's dismissed without signing in - so a brief
        // server hiccup can never strand the user behind an unclosable window.
        ClientSize = new Size(380, 210);
        Font = new Font("Segoe UI", 9F);
        TopMost = true;

        var heading = new Label
        {
            Text = "Sign in to continue",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18),
        };

        var subheading = new Label
        {
            Text = $"Signed in as {employeeCode}. Enter your password to unlock the internet.",
            ForeColor = SystemColors.GrayText,
            AutoSize = false,
            Location = new Point(20, 46),
            Size = new Size(340, 34),
        };

        var passwordLabel = new Label { Text = "Password", AutoSize = true, Location = new Point(20, 88) };
        _passwordBox = new TextBox { Location = new Point(20, 108), Size = new Size(340, 24), PasswordChar = '●' };

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 140),
            Size = new Size(340, 18),
            ForeColor = Color.Firebrick,
        };

        _signInButton = new Button
        {
            Text = "Sign in",
            Location = new Point(20, 164),
            Size = new Size(340, 36),
            FlatStyle = FlatStyle.System,
        };
        _signInButton.Click += async (_, _) => await SignInAsync();

        AcceptButton = _signInButton;

        Controls.Add(heading);
        Controls.Add(subheading);
        Controls.Add(passwordLabel);
        Controls.Add(_passwordBox);
        Controls.Add(_statusLabel);
        Controls.Add(_signInButton);

        Shown += (_, _) => _passwordBox.Focus();
    }

    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(_passwordBox.Text))
        {
            SetBusy(false, "Enter your password.", Color.Firebrick);
            return;
        }

        SetBusy(true, "Signing in…", Color.DimGray);
        try
        {
            // Re-validate against the server with the remembered employee code +
            // license; on success this re-records the current boot session, and
            // the login-enforcement worker restores internet access.
            var result = await _loginService.LoginAndActivateAsync(_employeeCode, _passwordBox.Text, _licenseKey);
            if (result.Success)
            {
                Close();
                return;
            }
            SetBusy(false, result.Message, Color.Firebrick);
            _passwordBox.SelectAll();
            _passwordBox.Focus();
        }
        catch (Exception ex)
        {
            SetBusy(false, "Unexpected error: " + ex.Message, Color.Firebrick);
        }
    }

    private void SetBusy(bool busy, string status, Color color)
    {
        _signInButton.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        _statusLabel.ForeColor = color;
        _statusLabel.Text = status;
    }
}
