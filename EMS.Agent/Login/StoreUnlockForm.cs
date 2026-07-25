using System.Runtime.Versioning;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// "Unlock Microsoft Store" window. Collects an EMS admin's credentials,
/// verifies them against the server, and on success grants a temporary Store
/// unlock so the user can install. All logic lives in
/// <see cref="IStoreUnlockService"/>; this is just the form.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class StoreUnlockForm : Form
{
    private readonly IStoreUnlockService _unlockService;

    private readonly TextBox _employeeCodeBox;
    private readonly TextBox _passwordBox;
    private readonly Label _statusLabel;
    private readonly Button _unlockButton;

    public StoreUnlockForm(IStoreUnlockService unlockService)
    {
        _unlockService = unlockService;

        Text = "EMS Agent — Unlock Microsoft Store";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(400, 280);
        Font = new Font("Segoe UI", 9F);

        var heading = new Label
        {
            Text = "Unlock the Microsoft Store",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18),
        };

        var subheading = new Label
        {
            Text = "An EMS administrator must approve Store installs.",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(20, 46),
        };

        var codeLabel = new Label { Text = "Admin employee code", AutoSize = true, Location = new Point(20, 82) };
        _employeeCodeBox = new TextBox { Location = new Point(20, 102), Size = new Size(360, 24) };

        var passwordLabel = new Label { Text = "Password", AutoSize = true, Location = new Point(20, 138) };
        _passwordBox = new TextBox { Location = new Point(20, 158), Size = new Size(360, 24), PasswordChar = '●' };

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 190),
            Size = new Size(360, 20),
            ForeColor = Color.Firebrick,
        };

        _unlockButton = new Button
        {
            Text = "Unlock",
            Location = new Point(20, 216),
            Size = new Size(360, 36),
            FlatStyle = FlatStyle.System,
        };
        _unlockButton.Click += async (_, _) => await UnlockAsync();
        AcceptButton = _unlockButton;

        Controls.AddRange(new Control[]
        {
            heading, subheading, codeLabel, _employeeCodeBox,
            passwordLabel, _passwordBox, _statusLabel, _unlockButton,
        });
    }

    private async Task UnlockAsync()
    {
        SetBusy(true, "Verifying…", Color.DimGray);

        try
        {
            var result = await _unlockService.UnlockAsync(_employeeCodeBox.Text, _passwordBox.Text);

            if (result.Success)
            {
                MessageBox.Show(
                    result.Message + "\n\nYou can now install from the Microsoft Store. It may take up to a"
                    + " minute to become available, and re-locks automatically when the time is up.",
                    "Microsoft Store Unlocked",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
        _unlockButton.Enabled = !busy;
        _employeeCodeBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        _statusLabel.ForeColor = color;
        _statusLabel.Text = status;
    }
}
