using System.Runtime.Versioning;
using EMS.Agent.Branding;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// Step 1 of activation: the license-key card. Validates the tenant license
/// key against the server; on success it opens the employee sign-in window
/// (<see cref="LoginForm"/>) scoped to that tenant.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LicenseActivationForm : Form
{
    private readonly ILicenseActivationService _licenseService;
    private readonly IActivationLoginService _loginService;

    private readonly TextBox _licenseBox;
    private readonly Label _statusLabel;
    private readonly Button _continueButton;

    public LicenseActivationForm(
        ILicenseActivationService licenseService, IActivationLoginService loginService)
    {
        _licenseService = licenseService;
        _loginService = loginService;

        Text = "The Lance Endpoint — Activation";
        try { Icon = BrandingAssets.AppIcon(); } catch { /* non-fatal */ }
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(380, 300);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        var logo = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(150, 18),
            Size = new Size(80, 64),
        };
        try { logo.Image = BrandingAssets.Logo(); } catch { /* non-fatal */ }

        var heading = new Label
        {
            Text = "Activate The Lance Endpoint",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 92),
            Size = new Size(340, 24),
        };

        var subheading = new Label
        {
            Text = "Enter your license key to begin.",
            ForeColor = SystemColors.GrayText,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 118),
            Size = new Size(340, 20),
        };

        var licenseLabel = new Label
        {
            Text = "License key",
            AutoSize = true,
            Location = new Point(20, 156),
        };
        _licenseBox = new TextBox
        {
            Location = new Point(20, 178),
            Size = new Size(340, 24),
            CharacterCasing = CharacterCasing.Upper,
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 210),
            Size = new Size(340, 20),
            ForeColor = Color.Firebrick,
        };

        _continueButton = new Button
        {
            Text = "Continue",
            Location = new Point(20, 238),
            Size = new Size(340, 36),
            FlatStyle = FlatStyle.System,
        };
        _continueButton.Click += async (_, _) => await ValidateAsync();
        AcceptButton = _continueButton;

        Controls.Add(logo);
        Controls.Add(heading);
        Controls.Add(subheading);
        Controls.Add(licenseLabel);
        Controls.Add(_licenseBox);
        Controls.Add(_statusLabel);
        Controls.Add(_continueButton);
    }

    private async Task ValidateAsync()
    {
        SetBusy(true, "Checking license…", Color.DimGray);

        try
        {
            var result = await _licenseService.ValidateAsync(_licenseBox.Text);
            if (!result.Success)
            {
                SetBusy(false, result.Message, Color.Firebrick);
                return;
            }

            // License is valid → move to employee sign-in for this tenant.
            Hide();
            using (var login = new LoginForm(_loginService, _licenseBox.Text.Trim(), result.TenantName))
            {
                login.ShowDialog();
            }
            Close();
        }
        catch (Exception ex)
        {
            SetBusy(false, "Unexpected error: " + ex.Message, Color.Firebrick);
        }
    }

    private void SetBusy(bool busy, string status, Color color)
    {
        _continueButton.Enabled = !busy;
        _licenseBox.Enabled = !busy;
        _statusLabel.ForeColor = color;
        _statusLabel.Text = status;
    }
}
