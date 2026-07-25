using System.Runtime.Versioning;
using EMS.Agent.Services;

namespace EMS.Agent.Login;

/// <summary>
/// Activation login window shown after install. Collects EMS credentials,
/// verifies them against the server, and on success activates the device so
/// the background service begins working. The window keeps no state beyond
/// the fields; all logic lives in <see cref="IActivationLoginService"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LoginForm : Form
{
    private readonly IActivationLoginService _loginService;

    private readonly TextField _usernameField;
    private readonly TextField _passwordField;
    private readonly Label _statusLabel;
    private readonly Button _activateButton;

    public LoginForm(IActivationLoginService loginService)
    {
        _loginService = loginService;

        Text = "EMS Agent — Activation";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(380, 260);
        Font = new Font("Segoe UI", 9F);

        var heading = new Label
        {
            Text = "Sign in to activate EMS",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18),
        };

        var subheading = new Label
        {
            Text = "Use your EMS dashboard account.",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(20, 46),
        };

        _usernameField = new TextField("Username or email", new Point(20, 78), passwordChar: '\0');
        _passwordField = new TextField("Password", new Point(20, 128), passwordChar: '●');

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 172),
            Size = new Size(340, 20),
            ForeColor = Color.Firebrick,
        };

        _activateButton = new Button
        {
            Text = "Activate",
            Location = new Point(20, 198),
            Size = new Size(340, 36),
            FlatStyle = FlatStyle.System,
        };
        _activateButton.Click += async (_, _) => await ActivateAsync();

        AcceptButton = _activateButton;

        Controls.Add(heading);
        Controls.Add(subheading);
        _usernameField.AddTo(Controls);
        _passwordField.AddTo(Controls);
        Controls.Add(_statusLabel);
        Controls.Add(_activateButton);
    }

    private async Task ActivateAsync()
    {
        SetBusy(true, "Signing in…", Color.DimGray);

        try
        {
            var result = await _loginService.LoginAndActivateAsync(
                _usernameField.Value, _passwordField.Value);

            if (result.Success)
            {
                _statusLabel.ForeColor = Color.Green;
                _statusLabel.Text = result.Message;
                MessageBox.Show(
                    result.Message + "\n\nThe EMS Agent is now active on this device.",
                    "EMS Activated",
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
        _activateButton.Enabled = !busy;
        _usernameField.Enabled = !busy;
        _passwordField.Enabled = !busy;
        _statusLabel.ForeColor = color;
        _statusLabel.Text = status;
    }

    /// <summary>A label + textbox pair, kept together for tidy layout.</summary>
    private sealed class TextField
    {
        private readonly Label _label;
        private readonly TextBox _textBox;

        public TextField(string caption, Point location, char passwordChar)
        {
            _label = new Label
            {
                Text = caption,
                AutoSize = true,
                Location = location,
            };
            _textBox = new TextBox
            {
                Location = new Point(location.X, location.Y + 20),
                Size = new Size(340, 24),
            };
            if (passwordChar != '\0')
            {
                _textBox.PasswordChar = passwordChar;
            }
        }

        public string Value => _textBox.Text;

        public bool Enabled
        {
            set => _textBox.Enabled = value;
        }

        public void AddTo(Control.ControlCollection controls)
        {
            controls.Add(_label);
            controls.Add(_textBox);
        }
    }
}
