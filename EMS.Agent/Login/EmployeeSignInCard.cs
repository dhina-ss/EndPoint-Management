using System.Runtime.Versioning;
using EMS.Agent.Branding;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// Shared employee code/email + password card layout and sign-in execution used
/// by both <see cref="LoginForm"/> and <see cref="SessionLoginForm"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class EmployeeSignInCard
{
    private readonly IActivationLoginService _loginService;
    private readonly string _licenseKey;
    private readonly Action<EmployeeSignInCard, ActivationLoginResult> _onSuccess;

    private readonly TextBox _identifierBox;
    private readonly TextBox _passwordBox;
    private readonly Label _statusLabel;
    private readonly Button _submitButton;

    public EmployeeSignInCard(
        Form form,
        IActivationLoginService loginService,
        string licenseKey,
        string title,
        string headingText,
        string subheadingText,
        string submitButtonText,
        bool topMost,
        Action<EmployeeSignInCard, ActivationLoginResult> onSuccess)
    {
        _loginService = loginService;
        _licenseKey = licenseKey;
        _onSuccess = onSuccess;

        form.Text = title;
        try { form.Icon = BrandingAssets.AppIcon(); } catch { /* non-fatal */ }
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ClientSize = new Size(380, 290);
        form.Font = new Font("Segoe UI", 9F);
        form.TopMost = topMost;

        var heading = new Label
        {
            Text = headingText,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18),
        };

        var subheading = new Label
        {
            Text = subheadingText,
            ForeColor = SystemColors.GrayText,
            AutoSize = false,
            Location = new Point(20, 46),
            Size = new Size(340, 36),
        };

        var identifierLabel = new Label
        {
            Text = "Employee code or email address",
            AutoSize = true,
            Location = new Point(20, 88),
        };
        _identifierBox = new TextBox
        {
            Location = new Point(20, 108),
            Size = new Size(340, 24),
        };

        var passwordLabel = new Label
        {
            Text = "Password",
            AutoSize = true,
            Location = new Point(20, 142),
        };
        _passwordBox = new TextBox
        {
            Location = new Point(20, 162),
            Size = new Size(340, 24),
            PasswordChar = '●',
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 194),
            Size = new Size(340, 34),
            ForeColor = Color.Firebrick,
        };

        _submitButton = new Button
        {
            Text = submitButtonText,
            Location = new Point(20, 234),
            Size = new Size(340, 36),
            FlatStyle = FlatStyle.System,
        };
        _submitButton.Click += async (_, _) => await SubmitAsync();

        form.AcceptButton = _submitButton;

        form.Controls.AddRange(new Control[]
        {
            heading,
            subheading,
            identifierLabel,
            _identifierBox,
            passwordLabel,
            _passwordBox,
            _statusLabel,
            _submitButton,
        });

        form.Shown += (_, _) => _identifierBox.Focus();
    }

    internal void SetStatus(string message, Color color)
    {
        _statusLabel.ForeColor = color;
        _statusLabel.Text = message;
    }

    internal async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_identifierBox.Text))
        {
            SetBusy(false, "Enter your employee code or email address.", Color.Firebrick);
            _identifierBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_passwordBox.Text))
        {
            SetBusy(false, "Enter your password.", Color.Firebrick);
            _passwordBox.Focus();
            return;
        }

        SetBusy(true, "Signing in…", Color.DimGray);
        try
        {
            var result = await _loginService.LoginAndActivateAsync(
                _identifierBox.Text, _passwordBox.Text, _licenseKey);

            if (result.Success)
            {
                _onSuccess(this, result);
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
        _submitButton.Enabled = !busy;
        _identifierBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        _statusLabel.ForeColor = color;
        _statusLabel.Text = status;
    }
}
