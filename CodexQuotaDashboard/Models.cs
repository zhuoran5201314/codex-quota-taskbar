using System.Text.Json.Serialization;

namespace CodexQuotaDashboard;

public sealed class DashboardSettings
{
    public bool AutoStart { get; set; } = true;
    public bool AnimationEnabled { get; set; } = true;
    public string RingColor { get; set; } = "#00A8FF";
    public string TextColor { get; set; } = "#FFFFFF";
    public double RingThickness { get; set; } = 3.2;
    public double BreathingPeriodSeconds { get; set; } = 1.8;
    public double BreathingMinimumOpacity { get; set; } = 0.45;
    public int HoverShowDelayMs { get; set; } = 250;
    public int HoverHideDelayMs { get; set; } = 350;
    public double PanelOpacity { get; set; } = 0.90;
    public int IdleRefreshMinutes { get; set; } = 15;
    public int MinimumRefreshMinutes { get; set; } = 2;
    public bool CheckUpdates { get; set; } = true;
}

public sealed class QuotaSnapshot
{
    public double? UsedPercent { get; set; }
    [JsonIgnore] public double? RemainingPercent => UsedPercent is null ? null : Math.Clamp(100 - UsedPercent.Value, 0, 100);
    public DateTimeOffset? ResetsAt { get; set; }
    public int? WindowMinutes { get; set; }
    public string PlanType { get; set; } = "";
    public string Model { get; set; } = "";
    public string ReasoningEffort { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.MinValue;
    public string Source { get; set; } = "";
    public string Error { get; set; } = "";
    [JsonIgnore] public bool IsAvailable => RemainingPercent is not null;
}

public sealed class ActivitySnapshot
{
    public bool IsRunning { get; set; }
    public int ActiveCount { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public string Model { get; set; } = "";
    public string ReasoningEffort { get; set; } = "";
    public string Stage { get; set; } = "空闲";
}
