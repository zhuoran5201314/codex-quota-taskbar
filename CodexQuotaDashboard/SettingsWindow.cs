using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexQuotaDashboard;

public enum SettingsPreviewTarget
{
    Ring,
    Breathing,
    HoverPanel
}

public sealed class SettingsWindow : Window
{
    private readonly DashboardSettings _working;
    private readonly CheckBox _autoStart = Check("开机时自动启动");
    private readonly CheckBox _animation = Check("任务执行时启用呼吸动画");
    private readonly CheckBox _updates = Check("启动时检查更新");
    private readonly Slider _thickness = Slider(2, 8, 0.1);
    private readonly Slider _minimumOpacity = Slider(0.15, 0.8, 0.05);
    private readonly Slider _panelOpacity = Slider(0.35, 0.98, 0.01);
    private readonly TextBlock _thicknessValue = ValueText();
    private readonly TextBlock _minimumOpacityValue = ValueText();
    private readonly TextBlock _panelOpacityValue = ValueText();
    private readonly TextBox _showDelay = Input();
    private readonly TextBox _hideDelay = Input();
    private bool _ready;
    private bool _saved;

    public event Action<DashboardSettings, SettingsPreviewTarget>? PreviewChanged;
    public event Action<DashboardSettings>? Saved;
    public event Action? PreviewEnded;

    public SettingsWindow(DashboardSettings settings)
    {
        _working = Copy(settings);
        Title = "Codex 额度仪表盘 · 设置";
        Width = 590;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
        FontFamily = new FontFamily("Microsoft YaHei UI");

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var content = new StackPanel();
        scroll.Content = content;

        content.Children.Add(Header("常规", "随系统启动与版本策略"));
        _autoStart.IsChecked = settings.AutoStart;
        _updates.IsChecked = settings.CheckUpdates;
        content.Children.Add(_autoStart);
        content.Children.Add(_updates);

        content.Children.Add(Header("固定显示", "无文字额度圆环与任务状态"));
        _animation.IsChecked = settings.AnimationEnabled;
        content.Children.Add(_animation);
        _thickness.Value = settings.RingThickness;
        _minimumOpacity.Value = settings.BreathingMinimumOpacity;
        content.Children.Add(SliderRow("圆环粗细", _thickness, _thicknessValue, "2.0 – 8.0"));
        content.Children.Add(SliderRow("呼吸最低透明度", _minimumOpacity, _minimumOpacityValue, "拖动时在托盘预览"));

        content.Children.Add(Header("悬浮面板", "Windows 10 Acrylic 毛玻璃"));
        _panelOpacity.Value = settings.PanelOpacity;
        content.Children.Add(SliderRow("面板不透明度", _panelOpacity, _panelOpacityValue, "拖动时自动显示面板"));
        _showDelay.Text = settings.HoverShowDelayMs.ToString();
        _hideDelay.Text = settings.HoverHideDelayMs.ToString();
        content.Children.Add(Row("出现延迟（毫秒）", _showDelay, "默认 250"));
        content.Children.Add(Row("消失延迟（毫秒）", _hideDelay, "默认 350"));

        content.Children.Add(Header("数据", "额度查询不会消耗 token"));
        content.Children.Add(Note("启动时、任务状态变化后及空闲每 15 分钟刷新；两次自动查询至少间隔 2 分钟。离线时保留最后一次有效数据。"));

        root.Children.Add(scroll);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var reset = Button("恢复默认", false);
        reset.Click += (_, _) => ApplyDefaults();
        var cancel = Button("取消", false);
        cancel.Click += (_, _) => Close();
        var save = Button("保存", true);
        save.Click += (_, _) => Save();
        buttons.Children.Add(reset);
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        Content = root;

        _thickness.ValueChanged += (_, _) => OnSliderChanged(SettingsPreviewTarget.Ring);
        _minimumOpacity.ValueChanged += (_, _) => OnSliderChanged(SettingsPreviewTarget.Breathing);
        _panelOpacity.ValueChanged += (_, _) => OnSliderChanged(SettingsPreviewTarget.HoverPanel);
        Closed += (_, _) =>
        {
            if (!_saved) PreviewEnded?.Invoke();
        };
        UpdateValueLabels();
        _ready = true;
    }

    private void OnSliderChanged(SettingsPreviewTarget target)
    {
        UpdateValueLabels();
        if (!_ready) return;
        UpdateWorkingFromControls();
        PreviewChanged?.Invoke(Copy(_working), target);
    }

    private void UpdateValueLabels()
    {
        _thicknessValue.Text = $"{_thickness.Value:0.0}";
        _minimumOpacityValue.Text = $"{_minimumOpacity.Value:P0}";
        _panelOpacityValue.Text = $"{_panelOpacity.Value:P0}";
    }

    private void Save()
    {
        UpdateWorkingFromControls();
        _working.AutoStart = _autoStart.IsChecked == true;
        _working.CheckUpdates = _updates.IsChecked == true;
        _working.AnimationEnabled = _animation.IsChecked == true;
        _working.HoverShowDelayMs = ParseInt(_showDelay.Text, 250, 0, 2000);
        _working.HoverHideDelayMs = ParseInt(_hideDelay.Text, 350, 0, 3000);
        _saved = true;
        Saved?.Invoke(Copy(_working));
        Close();
    }

    private void UpdateWorkingFromControls()
    {
        _working.RingThickness = Math.Round(_thickness.Value, 1);
        _working.BreathingMinimumOpacity = Math.Round(_minimumOpacity.Value, 2);
        _working.PanelOpacity = Math.Round(_panelOpacity.Value, 2);
    }

    private void ApplyDefaults()
    {
        var value = new DashboardSettings();
        _autoStart.IsChecked = value.AutoStart;
        _updates.IsChecked = value.CheckUpdates;
        _animation.IsChecked = value.AnimationEnabled;
        _thickness.Value = value.RingThickness;
        _minimumOpacity.Value = value.BreathingMinimumOpacity;
        _panelOpacity.Value = value.PanelOpacity;
        _showDelay.Text = value.HoverShowDelayMs.ToString();
        _hideDelay.Text = value.HoverHideDelayMs.ToString();
        UpdateWorkingFromControls();
        UpdateValueLabels();
    }

    private static Border Header(string title, string subtitle)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(115, 124, 140))
        });
        return new Border
        {
            Child = stack,
            Margin = new Thickness(0, 16, 0, 8),
            Padding = new Thickness(0, 0, 0, 7),
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 223, 232)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
    }

    private static Grid SliderRow(string name, Slider slider, TextBlock value, string note)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition());
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        panel.Children.Add(slider);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(value, 1);
        panel.Children.Add(value);
        return Row(name, panel, note);
    }

    private static Grid Row(string name, UIElement control, string note)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        var hint = new TextBlock
        {
            Text = note, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(125, 134, 150)), FontSize = 10, TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(hint, 2);
        grid.Children.Add(hint);
        return grid;
    }

    private static TextBlock Note(string value) => new()
    {
        Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 11,
        Foreground = new SolidColorBrush(Color.FromRgb(95, 106, 125)), Margin = new Thickness(0, 3, 0, 8)
    };

    private static TextBlock ValueText() => new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromRgb(44, 54, 72))
    };

    private static CheckBox Check(string value) => new()
    {
        Content = value, Margin = new Thickness(0, 4, 0, 5), VerticalContentAlignment = VerticalAlignment.Center
    };

    private static TextBox Input() => new()
    {
        Height = 27, Padding = new Thickness(7, 3, 7, 3), VerticalContentAlignment = VerticalAlignment.Center
    };

    private static Slider Slider(double min, double max, double tick) => new()
    {
        Minimum = min, Maximum = max, TickFrequency = tick, IsSnapToTickEnabled = true,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static System.Windows.Controls.Button Button(string text, bool primary) => new()
    {
        Content = text, MinWidth = 82, Height = 31, Margin = new Thickness(8, 0, 0, 0),
        Background = new SolidColorBrush(primary ? Color.FromRgb(0, 143, 230) : Color.FromRgb(226, 230, 237)),
        Foreground = primary ? Brushes.White : Brushes.Black,
        BorderThickness = new Thickness(0)
    };

    private static int ParseInt(string text, int fallback, int min, int max) =>
        int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : fallback;

    private static DashboardSettings Copy(DashboardSettings value) => new()
    {
        AutoStart = value.AutoStart,
        AnimationEnabled = value.AnimationEnabled,
        RingColor = value.RingColor,
        TextColor = value.TextColor,
        RingThickness = value.RingThickness,
        BreathingPeriodSeconds = value.BreathingPeriodSeconds,
        BreathingMinimumOpacity = value.BreathingMinimumOpacity,
        HoverShowDelayMs = value.HoverShowDelayMs,
        HoverHideDelayMs = value.HoverHideDelayMs,
        PanelOpacity = value.PanelOpacity,
        IdleRefreshMinutes = value.IdleRefreshMinutes,
        MinimumRefreshMinutes = value.MinimumRefreshMinutes,
        CheckUpdates = value.CheckUpdates
    };
}
