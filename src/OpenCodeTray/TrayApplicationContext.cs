using System.Diagnostics;
using System.Windows.Forms;

namespace OpenCodeTray;

/// <summary>Owns the tray icon, context menu, refresh timer and hover popup.</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _popupTimer;
    private readonly HoverPopupForm _popup;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly ToolStripMenuItem _startupItem;

    private AppSettings _settings;
    private UsageSnapshot? _snapshot;
    private string? _error;
    private bool _wasRateLimited;
    private bool _disposed;
    private bool _menuOpen;

    public TrayApplicationContext(AppSettings settings)
    {
        _settings = settings;

        _popup = new HoverPopupForm();
        _popupTimer = new System.Windows.Forms.Timer { Interval = 12000 };
        _popupTimer.Tick += (_, _) => HidePopup();
        _popup.MouseEnter += (_, _) => _popupTimer.Stop();
        _popup.MouseLeave += (_, _) =>
        {
            _popupTimer.Stop();
            _popupTimer.Interval = 350;
            _popupTimer.Start();
        };

        _statusItem = new ToolStripMenuItem("Ładowanie…") { Enabled = false };
        _refreshItem = new ToolStripMenuItem("Odśwież teraz", null, async (_, _) => await RefreshAsync());
        var settingsItem = new ToolStripMenuItem("Ustawienia…", null, (_, _) => OpenSettings());
        var consoleItem = new ToolStripMenuItem("Otwórz konsolę OpenCode…", null, (_, _) => OpenConsole());
        _startupItem = new ToolStripMenuItem("Uruchamiaj przy starcie Windows", null, (_, _) => ToggleStartup());
        var exitItem = new ToolStripMenuItem("Zakończ", null, (_, _) => ExitApp());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_refreshItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(consoleItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        // Keep the popup from floating above the context menu: while the menu is
        // open the tray icon keeps receiving WM_MOUSEMOVE, which would re-show it.
        menu.Opening += (_, _) =>
        {
            _menuOpen = true;
            HidePopup();
        };
        menu.Closed += (_, _) => _menuOpen = false;

        _notifyIcon = new NotifyIcon
        {
            Icon = IconResources.Normal,
            Text = "OpenCode Go",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.MouseMove += (_, _) => ShowPopup();
        _notifyIcon.MouseDown += (_, _) => HidePopup();
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                HidePopup();
                _ = RefreshAsync();
            }
        };

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(1, _settings.RefreshMinutes) * 60_000,
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();

        _startupItem.Checked = StartupManager.IsEnabled();
        UpdateUi();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            _refreshItem.Enabled = false;

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _snapshot = null;
                _error = "Nie skonfigurowano klucza API.\nKliknij „Ustawienia…”, aby go dodać.";
                UpdateUi();
                return;
            }

            try
            {
                var snapshot = await UsageClient.FetchAsync(_settings.ApiKey);
                _snapshot = snapshot;
                _error = null;
                NotifyIfRateLimited(snapshot);
            }
            catch (UsageException ex)
            {
                _error = ex.Message;
            }
            catch (Exception ex)
            {
                _error = $"Nieoczekiwany błąd: {ex.Message}";
            }
            finally
            {
                UpdateUi();
            }
        }
        finally
        {
            _refreshItem.Enabled = true;
            _refreshGate.Release();
        }
    }

    private void UpdateUi()
    {
        string tooltip;
        bool rateLimited = false;

        if (_snapshot is not null)
        {
            var s = _snapshot;
            rateLimited = s.IsAnyRateLimited;
            tooltip = $"OpenCode Go • {s.FetchedAt:HH:mm}\n" +
                      $"5h: {FormatShort(s.Rolling)}  |  Tydz: {FormatShort(s.Weekly)}  |  Mies: {FormatShort(s.Monthly)}";
            _statusItem.Text = $"5h {FormatShort(s.Rolling)}   •   tydzień {FormatShort(s.Weekly)}   •   miesiąc {FormatShort(s.Monthly)}";
        }
        else if (_error is not null)
        {
            tooltip = "OpenCode Go — brak danych\n" + FirstLine(_error);
            _statusItem.Text = FirstLine(_error);
        }
        else
        {
            tooltip = "OpenCode Go";
            _statusItem.Text = "Brak danych";
        }

        _notifyIcon.Text = Truncate(tooltip, 127);
        _notifyIcon.Icon = rateLimited ? IconResources.Alert : IconResources.Normal;
        _startupItem.Checked = StartupManager.IsEnabled();

        if (_popup.Visible)
        {
            _popup.UpdateContent(_snapshot, _error);
        }
    }

    private void NotifyIfRateLimited(UsageSnapshot snapshot)
    {
        if (snapshot.IsAnyRateLimited && !_wasRateLimited && _settings.ShowNotifications)
        {
            _notifyIcon.BalloonTipTitle = "OpenCode Go — limit osiągnięty";
            _notifyIcon.BalloonTipText = "Wykorzystano limit OpenCode Go. Sprawdź szczegóły w ikonie w zasobniku.";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
            _notifyIcon.ShowBalloonTip(8000);
        }

        _wasRateLimited = snapshot.IsAnyRateLimited;
    }

    private void ShowPopup()
    {
        if (_menuOpen || _disposed)
        {
            return;
        }

        if (_snapshot is null && _error is null)
        {
            return;
        }

        _popup.UpdateContent(_snapshot, _error);
        _popup.ShowNearCursor();
        _popupTimer.Stop();
        _popupTimer.Interval = 12000;
        _popupTimer.Start();
    }

    private void HidePopup()
    {
        _popupTimer.Stop();
        if (_popup.Visible)
        {
            _popup.Hide();
        }
    }

    private void OpenSettings()
    {
        HidePopup();
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK || form.Result is null)
        {
            return;
        }

        _settings = form.Result;
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Nie udało się zapisać ustawień: {ex.Message}",
                "OpenCode Tray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        StartupManager.SetEnabled(_settings.StartWithWindows);
        _refreshTimer.Interval = Math.Max(1, _settings.RefreshMinutes) * 60_000;
        _startupItem.Checked = StartupManager.IsEnabled();
        _ = RefreshAsync();
    }

    private void ToggleStartup()
    {
        var enable = !StartupManager.IsEnabled();
        StartupManager.SetEnabled(enable);
        _settings.StartWithWindows = enable;
        try
        {
            _settings.Save();
        }
        catch
        {
            // Ignore; the registry value is already applied.
        }

        _startupItem.Checked = StartupManager.IsEnabled();
    }

    private static void OpenConsole()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://opencode.ai/auth",
                UseShellExecute = true,
            });
        }
        catch
        {
            // Ignore failures to open the browser.
        }
    }

    private void ExitApp()
    {
        _disposed = true;
        _refreshTimer.Stop();
        _popupTimer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _popup.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _refreshTimer.Dispose();
            _popupTimer.Dispose();
            _notifyIcon.Dispose();
            _popup.Dispose();
            _refreshGate.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string FormatShort(UsageWindow window) =>
        window.IsRateLimited ? "limit" : $"{Math.Round(window.Percent)}%";

    private static string FirstLine(string text)
    {
        var index = text.IndexOf('\n');
        return index < 0 ? text : text[..index];
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
