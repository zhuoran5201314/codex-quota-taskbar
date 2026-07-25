using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CodexQuotaDashboard;

public sealed class HoverWindow : Window
{
    private readonly TextBlock _remaining = Text("");
    private readonly TextBlock _reset = Text("");
    private readonly TextBlock _countdown = Text("");
    private readonly TextBlock _status = Text("");
    private readonly TextBlock _task = Text("");
    private readonly TextBlock _model = Text("");
    private readonly TextBlock _sync = Text("");
    private readonly Border _accent;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private QuotaSnapshot _quota = new();
    private ActivitySnapshot _activity = new();
    private DashboardSettings _settings;

    public event Action? PointerEntered;
    public event Action? PointerLeft;

    public HoverWindow(DashboardSettings settings)
    {
        _settings = settings;
        Width = 332;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowsTransparency = false;
        Foreground = Brushes.White;

        var root = new Border
        {
            Padding = new Thickness(18, 15, 18, 14),
            BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
            BorderThickness = new Thickness(1)
        };
        var stack = new StackPanel();
        root.Child = stack;

        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition());
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.Children.Add(Text("CODEX WEEKLY", 11, FontWeights.SemiBold, Color.FromRgb(139, 151, 175)));
        _status.FontSize = 11;
        Grid.SetColumn(_status, 1);
        titleRow.Children.Add(_status);
        stack.Children.Add(titleRow);

        var quotaRow = new Grid { Margin = new Thickness(0, 7, 0, 2) };
        quotaRow.ColumnDefinitions.Add(new ColumnDefinition());
        quotaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _remaining.FontSize = 30;
        _remaining.FontWeight = FontWeights.SemiBold;
        quotaRow.Children.Add(_remaining);
        var resetStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _reset.TextAlignment = TextAlignment.Right;
        _reset.Foreground = new SolidColorBrush(Color.FromRgb(190, 196, 210));
        _countdown.TextAlignment = TextAlignment.Right;
        _countdown.Foreground = new SolidColorBrush(Color.FromRgb(130, 142, 164));
        resetStack.Children.Add(_reset);
        resetStack.Children.Add(_countdown);
        Grid.SetColumn(resetStack, 1);
        quotaRow.Children.Add(resetStack);
        stack.Children.Add(quotaRow);

        _accent = new Border
        {
            Height = 3,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 5, 0, 13)
        };
        stack.Children.Add(_accent);
        stack.Children.Add(Section("当前任务", _task));
        stack.Children.Add(Section("模型 / 推理", _model));
        stack.Children.Add(Section("数据同步", _sync));
        Content = root;

        SourceInitialized += (_, _) => ApplyVisualSettings();
        ContentRendered += (_, _) => ApplyVisualSettings();
        SizeChanged += (_, _) =>
        {
            if (IsVisible) ApplyVisualSettings();
        };
        MouseEnter += (_, _) => PointerEntered?.Invoke();
        MouseLeave += (_, _) => PointerLeft?.Invoke();
        _clock.Tick += (_, _) => RefreshText();
        _clock.Start();
        ApplyBackground();
    }

    public void UpdateData(QuotaSnapshot quota, ActivitySnapshot activity, DashboardSettings settings)
    {
        _quota = quota;
        _activity = activity;
        _settings = settings;
        RefreshText();
        ApplyVisualSettings();
    }

    public void PreviewSettings(DashboardSettings settings)
    {
        _settings = settings;
        ApplyVisualSettings();
        RefreshText();
    }

    public void ShowNearTray()
    {
        RefreshText();
        if (!IsVisible) Show();
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 10;
        Top = area.Bottom - ActualHeight - 10;
        ApplyVisualSettings();
    }

    private void ApplyVisualSettings()
    {
        ApplyBackground();
        NativeGlass.Apply(this, _settings.PanelOpacity, 14);
    }

    private void ApplyBackground()
    {
        var alpha = (byte)Math.Clamp(Math.Round(_settings.PanelOpacity * 255), 0, 255);
        Background = new SolidColorBrush(Color.FromArgb(alpha, 24, 26, 32));
    }

    private void RefreshText()
    {
        _remaining.Text = _quota.RemainingPercent is double remaining ? $"{remaining:0}% 剩余" : "--  暂无数据";
        _reset.Text = _quota.ResetsAt is DateTimeOffset reset ? $"重置 {reset.ToLocalTime():M月d日 HH:mm}" : "重置时间未知";
        _countdown.Text = _quota.ResetsAt is DateTimeOffset time ? FormatCountdown(time - DateTimeOffset.Now) : "";
        _status.Text = _quota.IsLive ? "● 已同步" : "● 缓存/离线";
        _status.Foreground = new SolidColorBrush(_quota.IsLive
            ? Color.FromRgb(97, 220, 164) : Color.FromRgb(170, 177, 190));
        _task.Text = _activity.IsRunning
            ? $"正在执行  ·  {_activity.ActiveCount} 个活动任务  ·  {FormatDuration(_activity.StartedAt)}"
            : _activity.Stage;
        var model = string.IsNullOrWhiteSpace(_activity.Model) ? "等待任务信息" : _activity.Model;
        var effort = string.IsNullOrWhiteSpace(_activity.ReasoningEffort) ? "" : $"  ·  {_activity.ReasoningEffort}";
        _model.Text = model + effort;
        _sync.Text = _quota.UpdatedAt == DateTimeOffset.MinValue
            ? _quota.Error
            : $"{_quota.Source}  ·  {_quota.UpdatedAt.LocalDateTime:HH:mm:ss}";
        var tier = TrayIconRenderer.GetRingColor(_quota.RemainingPercent);
        _accent.Background = new SolidColorBrush(Color.FromRgb(tier.R, tier.G, tier.B));
    }

    private static Border Section(string label, TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(Text(label, 12, FontWeights.Normal, Color.FromRgb(132, 143, 163)));
        value.FontSize = 12;
        value.Foreground = new SolidColorBrush(Color.FromRgb(229, 232, 240));
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);
        return new Border { Child = grid };
    }

    private static TextBlock Text(string value, double size = 12, FontWeight? weight = null, Color? color = null) => new()
    {
        Text = value,
        FontFamily = new FontFamily("Microsoft YaHei UI"),
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = new SolidColorBrush(color ?? Colors.White)
    };

    private static string FormatCountdown(TimeSpan value)
    {
        if (value <= TimeSpan.Zero) return "即将刷新";
        return $"还有 {(int)value.TotalDays}天 {value.Hours}小时 {value.Minutes}分";
    }

    private static string FormatDuration(DateTimeOffset? started) =>
        started is null
            ? "刚刚开始"
            : $"已运行 {(int)(DateTimeOffset.Now - started.Value).TotalMinutes:00}:{(DateTimeOffset.Now - started.Value).Seconds:00}";
}
