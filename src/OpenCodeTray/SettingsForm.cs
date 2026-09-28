using System.Drawing;
using System.Windows.Forms;

namespace OpenCodeTray;

/// <summary>Modal settings dialog with a "Szczegóły" tab.</summary>
public sealed class SettingsForm : Form
{
    private readonly AppSettings _original;
    private readonly UsageSnapshot? _initialSnapshot;

    private readonly TabControl _tabs = new();
    private readonly TabPage _tabSettings = new("Ustawienia");
    private readonly TabPage _tabDetails = new("Szczegóły");

    // Settings tab
    private readonly TextBox _apiKey = new();
    private readonly CheckBox _showKey = new();
    private readonly NumericUpDown _refresh = new();
    private readonly CheckBox _startWithWindows = new();
    private readonly CheckBox _showNotifications = new();
    private readonly Label _status = new();
    private readonly Button _testButton = new();

    // Details tab
    private readonly Label _detailsUpdated = new();
    private readonly Button _detailsRefresh = new();
    private readonly ListView _limitsList = new();
    private readonly GroupBox _limitsBox = new();
    private readonly GroupBox _notesBox = new();

    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();

    private bool _detailsLoaded;

    /// <summary>The edited settings. Only valid when the dialog returns OK.</summary>
    public AppSettings? Result { get; private set; }

    public SettingsForm(AppSettings current, UsageSnapshot? initialSnapshot = null)
    {
        _original = current;
        _initialSnapshot = initialSnapshot;

        Text = "Ustawienia — OpenCode Tray";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(528, 418);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(250, 250, 252);
        Padding = new Padding(8);

        BuildUi();
        Populate();

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
    }

    private void BuildUi()
    {
        BuildSettingsTab();
        BuildDetailsTab();

        _tabs.Location = new Point(12, 12);
        _tabs.Size = new Size(504, 350);
        _tabs.Padding = new Point(12, 6);
        _tabs.TabPages.Add(_tabSettings);
        _tabs.TabPages.Add(_tabDetails);
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_tabs.SelectedIndex == 1 && !_detailsLoaded)
            {
                _ = LoadDetailsAsync();
            }
        };

        _testButton.Text = "Testuj połączenie";
        _testButton.Location = new Point(12, 374);
        _testButton.Width = 160;
        _testButton.Height = 32;
        _testButton.Click += async (_, _) => await TestConnectionAsync();

        _saveButton.Text = "Zapisz";
        _saveButton.Location = new Point(292, 374);
        _saveButton.Width = 110;
        _saveButton.Height = 32;
        _saveButton.Click += (_, _) => Save();

        _cancelButton.Text = "Anuluj";
        _cancelButton.Location = new Point(406, 374);
        _cancelButton.Width = 110;
        _cancelButton.Height = 32;
        _cancelButton.DialogResult = DialogResult.Cancel;

        Controls.Add(_tabs);
        Controls.Add(_testButton);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);
    }

    private void BuildSettingsTab()
    {
        var keyLabel = new Label
        {
            Text = "Klucz API OpenCode Go:",
            Location = new Point(8, 10),
            AutoSize = true,
        };

        _apiKey.Location = new Point(8, 32);
        _apiKey.Size = new Size(470, 24);
        _apiKey.UseSystemPasswordChar = true;

        _showKey.Text = "Pokaż klucz";
        _showKey.Location = new Point(8, 62);
        _showKey.AutoSize = true;
        _showKey.CheckedChanged += (_, _) => _apiKey.UseSystemPasswordChar = !_showKey.Checked;

        var getKeyLink = new LinkLabel
        {
            Text = "Skąd wziąć klucz API? → opencode.ai/auth",
            Location = new Point(8, 86),
            AutoSize = true,
        };
        getKeyLink.LinkClicked += (_, _) => OpenUrl("https://opencode.ai/auth");

        var refreshLabel = new Label
        {
            Text = "Częstotliwość odświeżania:",
            Location = new Point(8, 118),
            AutoSize = true,
        };

        _refresh.Location = new Point(8, 140);
        _refresh.Width = 90;
        _refresh.Minimum = 1;
        _refresh.Maximum = 1440;
        _refresh.Increment = 1;

        var refreshUnit = new Label
        {
            Text = "minut (1–1440)",
            Location = new Point(104, 143),
            AutoSize = true,
        };

        _startWithWindows.Text = "Uruchamiaj automatycznie przy starcie systemu Windows";
        _startWithWindows.Location = new Point(8, 178);
        _startWithWindows.AutoSize = true;

        _showNotifications.Text = "Pokazuj powiadomienie, gdy limit zostanie osiągnięty";
        _showNotifications.Location = new Point(8, 204);
        _showNotifications.AutoSize = true;

        _status.Location = new Point(8, 236);
        _status.Size = new Size(470, 60);
        _status.ForeColor = Color.FromArgb(90, 90, 100);

        _tabSettings.Controls.AddRange(new Control[]
        {
            keyLabel, _apiKey, _showKey, getKeyLink,
            refreshLabel, _refresh, refreshUnit,
            _startWithWindows, _showNotifications, _status,
        });
    }

    private void BuildDetailsTab()
    {
        _detailsUpdated.Location = new Point(8, 8);
        _detailsUpdated.Size = new Size(374, 22);
        _detailsUpdated.AutoEllipsis = true;
        _detailsUpdated.ForeColor = Color.FromArgb(90, 90, 100);
        _detailsUpdated.Text = "Otwórz tę zakładkę, aby wczytać szczegóły.";

        _detailsRefresh.Text = "Odśwież";
        _detailsRefresh.Location = new Point(390, 6);
        _detailsRefresh.Size = new Size(88, 26);
        _detailsRefresh.Click += async (_, _) => await LoadDetailsAsync(force: true);

        _limitsBox.Text = "Wykorzystanie limitów OpenCode Go";
        _limitsBox.Location = new Point(8, 34);
        _limitsBox.Size = new Size(470, 172);

        _limitsList.View = View.Details;
        _limitsList.FullRowSelect = true;
        _limitsList.GridLines = true;
        _limitsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _limitsList.Location = new Point(14, 24);
        _limitsList.Size = new Size(440, 136);
        _limitsList.Columns.Add("Okno", 105);
        _limitsList.Columns.Add("Użycie", 62);
        _limitsList.Columns.Add("Status", 78);
        _limitsList.Columns.Add("Reset", 100);
        _limitsList.Columns.Add("Pozostało", 95);
        _limitsBox.Controls.Add(_limitsList);

        _notesBox.Text = "Zużycie dolarów, tokenów, requestów i modeli";
        _notesBox.Location = new Point(8, 214);
        _notesBox.Size = new Size(470, 94);

        var notes = new Label
        {
            Text = "API OpenCode Go udostępnia wyłącznie procent wykorzystania oraz czas resetu " +
                   "dla okien 5h / tydzień / miesiąc. Zużycie w dolarach, liczba tokenów i zapytań " +
                   "oraz lista użytych modeli nie są dostępne przez API — znajdziesz je w konsoli.",
            Location = new Point(14, 20),
            Size = new Size(442, 46),
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(90, 90, 100),
        };

        var consoleLink = new LinkLabel
        {
            Text = "Otwórz konsolę OpenCode (opencode.ai/auth) →",
            Location = new Point(14, 66),
            AutoSize = true,
        };
        consoleLink.LinkClicked += (_, _) => OpenUrl("https://opencode.ai/auth");

        _notesBox.Controls.Add(notes);
        _notesBox.Controls.Add(consoleLink);

        _tabDetails.Controls.Add(_detailsUpdated);
        _tabDetails.Controls.Add(_detailsRefresh);
        _tabDetails.Controls.Add(_limitsBox);
        _tabDetails.Controls.Add(_notesBox);
    }

    private void Populate()
    {
        _apiKey.Text = _original.ApiKey;
        _refresh.Value = Math.Clamp(_original.RefreshMinutes, 1, 1440);
        _startWithWindows.Checked = _original.StartWithWindows;
        _showNotifications.Checked = _original.ShowNotifications;
    }

    private async Task LoadDetailsAsync(bool force = false)
    {
        var key = _apiKey.Text.Trim();
        if (key.Length == 0)
        {
            PopulateDetails(null, "Nie podano klucza API.");
            _detailsLoaded = true;
            return;
        }

        _detailsRefresh.Enabled = false;
        try
        {
            if (!force && _initialSnapshot is not null)
            {
                PopulateDetails(_initialSnapshot, null);
            }

            var snapshot = await UsageClient.FetchAsync(key);
            PopulateDetails(snapshot, null);
        }
        catch (UsageException ex)
        {
            PopulateDetails(null, ex.Message);
        }
        catch (Exception ex)
        {
            PopulateDetails(null, $"Nieoczekiwany błąd: {ex.Message}");
        }
        finally
        {
            _detailsRefresh.Enabled = true;
            _detailsLoaded = true;
        }
    }

    private void PopulateDetails(UsageSnapshot? snapshot, string? error)
    {
        _limitsList.Items.Clear();

        if (snapshot is null)
        {
            _detailsUpdated.Text = error ?? "Brak danych.";
            _detailsUpdated.ForeColor = Color.FromArgb(180, 40, 60);
            return;
        }

        _detailsUpdated.Text = $"Stan na {snapshot.FetchedAt:dd.MM.yyyy HH:mm:ss}   (GET /zen/go/v1/usage)";
        _detailsUpdated.ForeColor = Color.FromArgb(90, 90, 100);

        AddRow("Okno 5-godzinne", snapshot.Rolling);
        AddRow("Tydzień", snapshot.Weekly);
        AddRow("Miesiąc", snapshot.Monthly);
    }

    private void AddRow(string name, UsageWindow window)
    {
        var item = new ListViewItem(name);
        item.SubItems.Add($"{Math.Round(window.Percent)} %");
        item.SubItems.Add(window.IsRateLimited ? "LIMIT" : "OK");
        item.SubItems.Add(window.ResetsAt.ToLocalTime().ToString("dd.MM HH:mm"));
        item.SubItems.Add(FormatRemaining(window.ResetsAt - DateTimeOffset.Now));

        if (window.IsRateLimited || window.Percent >= 90)
        {
            item.ForeColor = Color.FromArgb(180, 40, 60);
            item.Font = new Font(_limitsList.Font, FontStyle.Bold);
        }

        _limitsList.Items.Add(item);
    }

    private static string FormatRemaining(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return "teraz";
        }

        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays} d {span.Hours} h";
        }

        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours} h {span.Minutes} min";
        }

        return $"{Math.Max(1, (int)span.TotalMinutes)} min";
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
            PopulateDetails(snapshot, null);
            _detailsLoaded = true;
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
