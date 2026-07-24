using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CodexQuotaDashboard;

public sealed class DashboardController : IDisposable
{
    private readonly SettingsStore _store = new();
    private readonly CodexAppServerClient _client = new();
    private readonly SessionActivityMonitor _activityMonitor = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly DispatcherTimer _animationTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _showTimer = new();
    private readonly DispatcherTimer _hideTimer = new();
    private readonly DispatcherTimer _pointerTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly Forms.NotifyIcon _tray = new();
    private DashboardSettings _settings;
    private DashboardSettings? _previewSettings;
    private QuotaSnapshot _quota;
    private ActivitySnapshot _activity = new();
    private HoverWindow _hover;
    private SettingsWindow? _settingsWindow;
    private DateTimeOffset _lastQuery = DateTimeOffset.MinValue;
    private DateTimeOffset _lastFrame = DateTimeOffset.Now;
    private DateTimeOffset _suppressTrayUntil = DateTimeOffset.MinValue;
    private System.Drawing.Point _lastTrayCursor;
    private double _phase;
    private long _hoverGeneration;
    private long _scheduledHoverGeneration;
    private bool _trayHoverActive;
    private bool _settingsPreviewActive;
    private bool _breathingPreviewActive;
    private System.Drawing.Icon? _icon;

    public DashboardController()
    {
        _settings = _store.LoadSettings();
        _quota = _store.LoadCache() ?? new QuotaSnapshot { Source = "等待首次同步" };
        _hover = CreateHover();
        _showTimer.Tick += ShowTimerTick;
        _hideTimer.Tick += HideTimerTick;
        _pointerTimer.Tick += PointerTimerTick;
    }

    public void Start()
    {
        ConfigureTray();
        if (_settings.AutoStart)
        {
            try { StartupManager.SetEnabled(true); } catch { }
        }

        _activityMonitor.Changed += OnActivityChanged;
        _animationTimer.Tick += OnAnimation;
        _animationTimer.Start();
        _pointerTimer.Start();
        _idleTimer.Tick += async (_, _) =>
        {
            if (DateTimeOffset.Now - _lastQuery >= TimeSpan.FromMinutes(_settings.IdleRefreshMinutes))
                await RefreshAsync(false);
        };
        _idleTimer.Start();
        UpdateIcon();
        _ = RefreshAsync(true);
    }

    private HoverWindow CreateHover()
    {
        var window = new HoverWindow(_settings);
        window.PointerEntered += () => _hideTimer.Stop();
        window.PointerLeft += () =>
        {
            if (!_trayHoverActive && !_settingsPreviewActive) ScheduleHide();
        };
        return window;
    }

    private void ConfigureTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("立即刷新", null, async (_, _) => await RefreshAsync(true));
        menu.Items.Add("设置…", null, (_, _) => OpenSettings());
        var animation = new Forms.ToolStripMenuItem("暂停呼吸动画") { Checked = !_settings.AnimationEnabled };
        animation.Click += (_, _) =>
        {
            _settings.AnimationEnabled = !_settings.AnimationEnabled;
            animation.Checked = !_settings.AnimationEnabled;
            _store.SaveSettings(_settings);
            UpdateIcon();
        };
        menu.Items.Add(animation);
        var startup = new Forms.ToolStripMenuItem("开机自动启动") { Checked = _settings.AutoStart };
        startup.Click += (_, _) =>
        {
            _settings.AutoStart = !_settings.AutoStart;
            startup.Checked = _settings.AutoStart;
            try { StartupManager.SetEnabled(_settings.AutoStart); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "无法修改开机启动"); }
            _store.SaveSettings(_settings);
        };
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("检查更新", null, (_, _) => System.Windows.MessageBox.Show(
            "当前为便携初版。更新通道将在发布地址确定后启用；设置与缓存已按可保留升级设计。",
            "Codex 额度仪表盘", MessageBoxButton.OK, MessageBoxImage.Information));
        menu.Items.Add("关于", null, (_, _) => System.Windows.MessageBox.Show(
            "Codex 额度仪表盘 0.1.0\nWindows 10 x64 便携版\n\n额度查询不会调用模型，也不会消耗 token。",
            "关于", MessageBoxButton.OK, MessageBoxImage.Information));
        menu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.Text = "Codex 额度仪表盘";
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(OpenSettings);
        _tray.MouseMove += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(OnTrayMouseMove);
    }

    private void OnTrayMouseMove()
    {
        if (_settingsPreviewActive) return;
        var now = DateTimeOffset.Now;
        if (now < _suppressTrayUntil && !_trayHoverActive) return;

        _lastTrayCursor = Forms.Cursor.Position;
        _hideTimer.Stop();
        if (_trayHoverActive)
        {
            if (_hover.IsVisible) return;
            ScheduleShow();
            return;
        }

        _trayHoverActive = true;
        _hoverGeneration++;
        ScheduleShow();
    }

    private void ScheduleShow()
    {
        if (_hover.IsVisible || _showTimer.IsEnabled) return;
        _scheduledHoverGeneration = _hoverGeneration;
        _showTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, _settings.HoverShowDelayMs));
        _showTimer.Start();
    }

    private void ShowTimerTick(object? sender, EventArgs e)
    {
        _showTimer.Stop();
        if (!_trayHoverActive || _scheduledHoverGeneration != _hoverGeneration) return;
        _hover.UpdateData(_quota, _activity, _settings);
        _hover.ShowNearTray();
    }

    private void PointerTimerTick(object? sender, EventArgs e)
    {
        if (!_trayHoverActive || _settingsPreviewActive) return;
        var current = Forms.Cursor.Position;
        var dx = current.X - _lastTrayCursor.X;
        var dy = current.Y - _lastTrayCursor.Y;
        if (dx * dx + dy * dy <= 14 * 14) return;

        _trayHoverActive = false;
        _hoverGeneration++;
        _showTimer.Stop();
        if (!_hover.IsMouseOver) ScheduleHide();
    }

    private void ScheduleHide()
    {
        if (_settingsPreviewActive) return;
        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, _settings.HoverHideDelayMs));
        _hideTimer.Start();
    }

    private void HideTimerTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (_trayHoverActive || _hover.IsMouseOver || _settingsPreviewActive) return;

        _showTimer.Stop();
        _hoverGeneration++;
        _hover.Hide();
        _suppressTrayUntil = DateTimeOffset.Now.AddMilliseconds(300);
    }

    private void OnActivityChanged(ActivitySnapshot value)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            var wasRunning = _activity.IsRunning;
            _activity = value;
            _hover.UpdateData(_quota, _activity, _previewSettings ?? _settings);
            UpdateIcon();
            if (wasRunning && !value.IsRunning)
                await RefreshAsync(false);
        });
    }

    private async Task RefreshAsync(bool force)
    {
        if (!force && DateTimeOffset.Now - _lastQuery < TimeSpan.FromMinutes(_settings.MinimumRefreshMinutes))
            return;
        if (!await _refreshGate.WaitAsync(0)) return;
        try
        {
            _lastQuery = DateTimeOffset.Now;
            var value = await _client.ReadRateLimitsAsync(_shutdown.Token);
            if (value.IsAvailable)
            {
                _quota = value;
                _store.SaveCache(value);
            }
            else if (!_quota.IsAvailable)
            {
                _quota = value;
            }
            else
            {
                _quota.Error = value.Error;
                _quota.Source = "最近缓存";
            }
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateIcon();
                _hover.UpdateData(_quota, _activity, _previewSettings ?? _settings);
            });
        }
        finally { _refreshGate.Release(); }
    }

    private void OnAnimation(object? sender, EventArgs e)
    {
        var effective = _previewSettings ?? _settings;
        var shouldAnimate = _breathingPreviewActive || (_activity.IsRunning && effective.AnimationEnabled);
        if (!shouldAnimate) return;

        var now = DateTimeOffset.Now;
        var delta = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        _phase = (_phase + delta / effective.BreathingPeriodSeconds * Math.PI * 2) % (Math.PI * 2);
        UpdateIcon(effective.BreathingMinimumOpacity +
                   (1 - effective.BreathingMinimumOpacity) * (Math.Sin(_phase - Math.PI / 2) + 1) / 2);
    }

    private void UpdateIcon(double opacity = 1)
    {
        var effective = _previewSettings ?? _settings;
        var next = TrayIconRenderer.Render(_quota, effective, opacity);
        var previous = _icon;
        _icon = next;
        _tray.Icon = next;
        var remaining = _quota.RemainingPercent;
        _tray.Text = remaining is null
            ? "Codex 额度：暂无数据"
            : $"Codex 周额度剩余 {remaining:0}%";
        previous?.Dispose();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.PreviewChanged += PreviewSettings;
        _settingsWindow.PreviewEnded += EndSettingsPreview;
        _settingsWindow.Saved += value =>
        {
            _settings = value;
            _store.SaveSettings(value);
            try { StartupManager.SetEnabled(value.AutoStart); } catch { }
            EndSettingsPreview();
            _hover.UpdateData(_quota, _activity, _settings);
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void PreviewSettings(DashboardSettings value, SettingsPreviewTarget target)
    {
        _previewSettings = value;
        switch (target)
        {
            case SettingsPreviewTarget.Ring:
                UpdateIcon();
                break;
            case SettingsPreviewTarget.Breathing:
                _breathingPreviewActive = true;
                _lastFrame = DateTimeOffset.Now;
                break;
            case SettingsPreviewTarget.HoverPanel:
                _settingsPreviewActive = true;
                _showTimer.Stop();
                _hideTimer.Stop();
                _hover.UpdateData(_quota, _activity, value);
                _hover.PreviewSettings(value);
                _hover.ShowNearTray();
                break;
        }
    }

    private void EndSettingsPreview()
    {
        _previewSettings = null;
        _breathingPreviewActive = false;
        _settingsPreviewActive = false;
        _hover.PreviewSettings(_settings);
        UpdateIcon();
        if (!_trayHoverActive && !_hover.IsMouseOver)
        {
            _hover.Hide();
            _suppressTrayUntil = DateTimeOffset.Now.AddMilliseconds(300);
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _activityMonitor.Dispose();
        _animationTimer.Stop();
        _idleTimer.Stop();
        _showTimer.Stop();
        _hideTimer.Stop();
        _pointerTimer.Stop();
        _hover.Close();
        _tray.Visible = false;
        _tray.Dispose();
        _icon?.Dispose();
        _shutdown.Dispose();
        _refreshGate.Dispose();
    }
}
