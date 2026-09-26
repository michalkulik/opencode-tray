using System.Drawing;
using System.Windows.Forms;

namespace OpenCodeTray;

/// <summary>Modal settings dialog.</summary>
public sealed class SettingsForm : Form
{
    private readonly AppSettings _original;

    private readonly TextBox _apiKey = new();
    private readonly CheckBox _showKey = new();
    private readonly NumericUpDown _refresh = new();
    private readonly CheckBox _startWithWindows = new();
    private readonly CheckBox _showNotifications = new();
    private readonly Label _status = new();
    private readonly Button _testButton = new();

    /// <summary>The edited settings. Only valid when the dialog returns OK.</summary>
    public AppSettings? Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        _original = current;

        Text = "Ustawienia — OpenCode Tray";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(492, 372);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(250, 250, 252);
        Padding = new Padding(16);

        BuildUi();
        Populate();

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
    }

    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();

    private void BuildUi()
    {
        var keyLabel = new Label
        {
            Text = "Klucz API OpenCode Go:",
            Location = new Point(16, 16),
            AutoSize = true,
        };

        _apiKey.Location = new Point(16, 40);
        _apiKey.Width = 460;
        _apiKey.UseSystemPasswordChar = true;

        _showKey.Text = "Pokaż klucz";
        _showKey.Location = new Point(16, 70);
        _showKey.AutoSize = true;
        _showKey.CheckedChanged += (_, _) => _apiKey.UseSystemPasswordChar = !_showKey.Checked;

        var getKeyLink = new LinkLabel
        {
            Text = "Skąd wziąć klucz API? → opencode.ai/auth",
            Location = new Point(16, 94),
            AutoSize = true,
        };
        getKeyLink.LinkClicked += (_, _) => OpenUrl("https://opencode.ai/auth");

        var refreshLabel = new Label
        {
            Text = "Częstotliwość odświeżania:",
            Location = new Point(16, 128),
            AutoSize = true,
        };

        _refresh.Location = new Point(16, 152);
        _refresh.Width = 90;
        _refresh.Minimum = 1;
        _refresh.Maximum = 1440;
        _refresh.Increment = 1;

        var refreshUnit = new Label
        {
            Text = "minut (1–1440)",
            Location = new Point(114, 155),
            AutoSize = true,
        };

        _startWithWindows.Text = "Uruchamiaj automatycznie przy starcie systemu Windows";
        _startWithWindows.Location = new Point(16, 190);
        _startWithWindows.AutoSize = true;

        _showNotifications.Text = "Pokazuj powiadomienie, gdy limit zostanie osiągnięty";
        _showNotifications.Location = new Point(16, 216);
        _showNotifications.AutoSize = true;

        _status.Location = new Point(16, 248);
        _status.Size = new Size(460, 52);
        _status.ForeColor = Color.FromArgb(90, 90, 100);

        _testButton.Text = "Testuj połączenie";
        _testButton.Location = new Point(16, 316);
        _testButton.Width = 150;
        _testButton.Height = 32;
        _testButton.Click += async (_, _) => await TestConnectionAsync();

        _saveButton.Text = "Zapisz";
        _saveButton.Location = new Point(284, 316);
        _saveButton.Width = 92;
        _saveButton.Height = 32;
        _saveButton.Click += (_, _) => Save();

        _cancelButton.Text = "Anuluj";
        _cancelButton.Location = new Point(384, 316);
        _cancelButton.Width = 92;
        _cancelButton.Height = 32;
        _cancelButton.DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[]
        {
            keyLabel, _apiKey, _showKey, getKeyLink,
            refreshLabel, _refresh, refreshUnit,
            _startWithWindows, _showNotifications, _status,
            _testButton, _saveButton, _cancelButton,
        });
    }

    private void Populate()
    {
        _apiKey.Text = _original.ApiKey;
        _refresh.Value = Math.Clamp(_original.RefreshMinutes, 1, 1440);
        _startWithWindows.Checked = _original.StartWithWindows;
        _showNotifications.Checked = _original.ShowNotifications;
    }

    private async Task TestConnectionAsync()
    {
        _testButton.Enabled = false;
        SetStatus("Sprawdzanie połączenia…", Color.FromArgb(90, 90, 100));
        try
        {
            var snapshot = await UsageClient.FetchAsync(_apiKey.Text);
            SetStatus(
                $"Połączenie OK. 5h: {Math.Round(snapshot.Rolling.Percent)}%, " +
                $"tydzień: {Math.Round(snapshot.Weekly.Percent)}%, " +
                $"miesiąc: {Math.Round(snapshot.Monthly.Percent)}%.",
                Color.FromArgb(30, 130, 70));
        }
        catch (UsageException ex)
        {
            SetStatus(ex.Message, Color.FromArgb(180, 40, 60));
        }
        catch (Exception ex)
        {
            SetStatus($"Nieoczekiwany błąd: {ex.Message}", Color.FromArgb(180, 40, 60));
        }
        finally
        {
            _testButton.Enabled = true;
        }
    }

    private void Save()
    {
        Result = new AppSettings
        {
            ApiKey = _apiKey.Text.Trim(),
            RefreshMinutes = (int)_refresh.Value,
            StartWithWindows = _startWithWindows.Checked,
            ShowNotifications = _showNotifications.Checked,
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Ignore failures to open the browser.
        }
    }
}
