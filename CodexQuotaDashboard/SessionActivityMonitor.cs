using System.Text.Json;

namespace CodexQuotaDashboard;

public sealed class SessionActivityMonitor : IDisposable
{
    private readonly string _sessionsRoot;
    private readonly FileSystemWatcher? _watcher;
    private readonly Dictionary<string, long> _positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeTurns = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly System.Threading.Timer _debounce;
    private ActivitySnapshot _current = new();

    public event Action<ActivitySnapshot>? Changed;

    public SessionActivityMonitor()
    {
        _sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
        _debounce = new System.Threading.Timer(_ => Scan(), null, Timeout.Infinite, Timeout.Infinite);
        if (Directory.Exists(_sessionsRoot))
        {
            _watcher = new FileSystemWatcher(_sessionsRoot, "*.jsonl")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            ScanInitialState();
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        lock (_gate)
            _debounce.Change(150, Timeout.Infinite);
    }

    private void ScanInitialState()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                         .Where(f => File.GetLastWriteTimeUtc(f) > DateTime.UtcNow.AddMinutes(-30))
                         .OrderByDescending(File.GetLastWriteTimeUtc).Take(12))
                ReadNewLines(file, fromBeginning: true);
        }
        catch { }
    }

    private void Scan()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                         .Where(f => File.GetLastWriteTimeUtc(f) > DateTime.UtcNow.AddMinutes(-10)))
                ReadNewLines(file, fromBeginning: false);
        }
        catch { }
    }

    private void ReadNewLines(string path, bool fromBeginning)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!fromBeginning && _positions.TryGetValue(path, out var position) && position <= stream.Length)
                stream.Position = position;
            else if (!fromBeginning && stream.Length > 8 * 1024 * 1024)
                stream.Position = stream.Length - 8 * 1024 * 1024;
            using var reader = new StreamReader(stream);
            if (stream.Position > 0 && !_positions.ContainsKey(path))
                reader.ReadLine(); // 丢弃从文件中间截到的半行
            string? line;
            while ((line = reader.ReadLine()) is not null) ProcessLine(line);
            _positions[path] = stream.Position;
        }
        catch { }
    }

    private void ProcessLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("payload", out var payload)) return;
            var outerType = root.TryGetProperty("type", out var outerNode) ? outerNode.GetString() : "";
            var type = payload.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : "";
            if (outerType == "turn_context")
            {
                _current.Model = ReadString(payload, "model");
                _current.ReasoningEffort = ReadString(payload, "effort", "reasoning_effort");
                Publish();
                return;
            }
            switch (type)
            {
                case "task_started":
                    var startedId = ReadString(payload, "turn_id");
                    if (!string.IsNullOrWhiteSpace(startedId)) _activeTurns.Add(startedId);
                    _current.ActiveCount = _activeTurns.Count;
                    _current.IsRunning = true;
                    if (_current.StartedAt is null)
                    {
                        var raw = ReadString(payload, "started_at");
                        _current.StartedAt = DateTimeOffset.TryParse(raw, out var startedAt)
                            ? startedAt : DateTimeOffset.Now;
                    }
                    _current.Stage = "正在执行";
                    Publish();
                    break;
                case "task_complete":
                case "turn_aborted":
                    var completedId = ReadString(payload, "turn_id");
                    if (!string.IsNullOrWhiteSpace(completedId)) _activeTurns.Remove(completedId);
                    _current.ActiveCount = _activeTurns.Count;
                    _current.IsRunning = _current.ActiveCount > 0;
                    if (!_current.IsRunning)
                    {
                        _current.StartedAt = null;
                        _current.Stage = type == "turn_aborted" ? "已中止" : "空闲";
                    }
                    Publish();
                    break;
                case "turn_context":
                    _current.Model = ReadString(payload, "model");
                    _current.ReasoningEffort = ReadString(payload, "effort", "reasoning_effort");
                    Publish();
                    break;
            }
        }
        catch { }
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? "";
        return "";
    }

    private void Publish() => Changed?.Invoke(new ActivitySnapshot
    {
        IsRunning = _current.IsRunning,
        ActiveCount = _current.ActiveCount,
        StartedAt = _current.StartedAt,
        Model = _current.Model,
        ReasoningEffort = _current.ReasoningEffort,
        Stage = _current.Stage
    });

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
