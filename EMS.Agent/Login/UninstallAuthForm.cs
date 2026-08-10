using System.Runtime.Versioning;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// "Authorize Uninstall" window shown by the installer's uninstaller. Collects
/// EMS admin credentials and verifies them against the server; the process exit
/// code (0 = authorized) tells the uninstaller whether to proceed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UninstallAuthForm : Form
{
    private readonly IAdminAuthService _authService;

    private readonly TextBox _employeeCodeBox;
    private readonly TextBox _passwordBox;
    private readonly Label _statusLabel;
    private readonly Button _authorizeButton;

    /// <summary>True only after the admin credentials verified successfully.</summary>
    public bool Authorized { get; private set; }

    public UninstallAuthForm(IAdminAuthService authService)
    {
        _authService = authService;

        Text = "EMS Agent — Authorize Uninstall";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(400, 300);
        Font = new Font("Segoe UI", 9F);

        var heading = new Label
        {
            Text = "Administrator authorization required",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18),
        };

        var subheading = new Label
        {
            Text = "Uninstalling EMS Agent requires EMS admin credentials.",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(20, 46),
        };

        var codeLabel = new Label { Text = "Admin employee code", AutoSize = true, Location = new Point(20, 86) };
        _employeeCodeBox = new TextBox { Location = new Point(20, 106), Size = new Size(360, 24) };

        var passwordLabel = new Label { Text = "Password", AutoSize = true, Location = new Point(20, 142) };
        _passwordBox = new TextBox { Location = new Point(20, 162), Size = new Size(360, 24), PasswordChar = '●' };

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 194),
            Size = new Size(360, 20),
            ForeColor = Color.Firebrick,
        };

        _authorizeButton = new Button
        {
            Text = "Authorize & Uninstall",
            Location = new Point(20, 220),
            Size = new Size(360, 36),
            FlatStyle = FlatStyle.System,
        };
        _authorizeButton.Click += async (_, _) => await AuthorizeAsync();
        AcceptButton = _authorizeButton;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(20, 262),
            Size = new Size(360, 28),
            FlatStyle = FlatStyle.System,
        };
        cancelButton.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            heading, subheading, codeLabel, _employeeCodeBox,
            passwordLabel, _passwordBox, _statusLabel, _authorizeButton, cancelButton,
        });
    }

    private async Task AuthorizeAsync()
    {
        SetBusy(true, "Verifying…", Color.DimGray);

        try
        {
            var result = await _authService.VerifyAsync(_employeeCodeBox.Text, _passwordBox.Text);
            if (result.Success)
            {
                Authorized = true;
                Close();
                return;
            }

            SetBusy(false, result.Message, Color.Firebrick);
        }
        catch (Exception ex)
        {
            SetBusy(false, "Unexpected error: " + ex.Message, Color.Firebrick);
        }
    }

    private void SetBusy(bool busy, string status, Color color)
    {
        _authorizeButton.Enabled = !busy;
        _employeeCodeBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        _statusLabel.ForeColor = color;
        _statusLabel.Text = status;
    }
}
