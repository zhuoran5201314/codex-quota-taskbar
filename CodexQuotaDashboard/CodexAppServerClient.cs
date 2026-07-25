using System.Diagnostics;
using System.Text.Json;

namespace CodexQuotaDashboard;

public sealed class CodexAppServerClient
{
    private int _requestId;

    public async Task<QuotaSnapshot> ReadRateLimitsAsync(CancellationToken cancellationToken)
    {
        var executable = LocateCodex();
        if (executable is null)
            return Error("未找到可访问的 Codex 命令行组件");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "app-server --listen stdio://",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };

        try
        {
            process.Start();
            var initializeId = NextId();
            await SendAsync(process, new
            {
                method = "initialize",
                id = initializeId,
                @params = new
                {
                    clientInfo = new { name = "codex-quota-dashboard", title = "Codex 额度仪表盘", version = "0.1.0" },
                    capabilities = new { experimentalApi = true }
                }
            });

            var initialized = await ReadResponseAsync(process, initializeId, TimeSpan.FromSeconds(5), cancellationToken);
            if (initialized is null)
                return Error("Codex 服务初始化超时");

            await SendAsync(process, new { method = "initialized", @params = new { } });
            var id = NextId();
            await SendAsync(process, new { method = "account/rateLimits/read", id, @params = new { } });
            var response = await ReadResponseAsync(process, id, TimeSpan.FromSeconds(8), cancellationToken);
            if (response is null)
                return Error("读取额度超时");
            if (response.RootElement.TryGetProperty("error", out var error))
                return Error(error.ToString());

            return ParseRateLimits(response.RootElement);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
        finally
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch { }
        }
    }

    private int NextId() => Interlocked.Increment(ref _requestId);

    private static async Task SendAsync(Process process, object message)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
        await process.StandardInput.FlushAsync();
    }

    private static async Task<JsonDocument?> ReadResponseAsync(
        Process process, int expectedId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        while (!linked.IsCancellationRequested)
        {
            string? line;
            try { line = await process.StandardOutput.ReadLineAsync(linked.Token); }
            catch (OperationCanceledException) { return null; }
            if (line is null) return null;
            try
            {
                var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.Number && id.GetInt32() == expectedId)
                    return doc;
                doc.Dispose();
            }
            catch (JsonException) { }
        }
        return null;
    }

    private static QuotaSnapshot ParseRateLimits(JsonElement response)
    {
        var root = response.TryGetProperty("result", out var result) ? result : response;
        var primary = FindObject(root, "primary") ?? FindObject(root, "primaryWindow");
        var secondary = FindObject(root, "secondary") ?? FindObject(root, "secondaryWindow");
        var chosen = secondary ?? primary ?? root;

        var used = FindNumber(chosen, "usedPercent", "used_percent", "percentUsed");
        var resetsRaw = FindNumber(chosen, "resetsAt", "resets_at", "resetAt");
        var window = FindNumber(chosen, "windowMinutes", "window_minutes");
        var plan = FindString(root, "planType", "plan_type", "plan");

        DateTimeOffset? resetsAt = null;
        if (resetsRaw is > 0)
        {
            var raw = resetsRaw.Value;
            try
            {
                resetsAt = raw > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds((long)raw)
                    : DateTimeOffset.FromUnixTimeSeconds((long)raw);
            }
            catch { }
        }

        return new QuotaSnapshot
        {
            UsedPercent = used,
            ResetsAt = resetsAt,
            WindowMinutes = window is null ? null : (int)window.Value,
            PlanType = plan ?? "",
            UpdatedAt = DateTimeOffset.Now,
            Source = "Codex 官方接口",
            Error = used is null ? "接口未返回可识别的额度窗口" : ""
        };
    }

    private static JsonElement? FindObject(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Object) return property.Value;
                var nested = FindObject(property.Value, name);
                if (nested is not null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindObject(child, name);
                if (nested is not null) return nested;
            }
        return null;
    }

    private static double? FindNumber(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(n => property.Name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
                        return number;
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        double.TryParse(property.Value.GetString(), out number)) return number;
                }
                var nested = FindNumber(property.Value, names);
                if (nested is not null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindNumber(child, names);
                if (nested is not null) return nested;
            }
        return null;
    }

    private static string? FindString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
        {
            if (names.Any(n => property.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind == JsonValueKind.String) return property.Value.GetString();
            var nested = FindString(property.Value, names);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static string? LocateCodex()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("CODEX_QUOTA_CODEX_PATH"),
            Path.Combine(home, ".codex", ".sandbox-bin", "codex.exe"),
            Path.Combine(home, ".codex", "bin", "codex.exe"),
            FindDesktopAppBinary(localAppData),
            FindRunningCodexBinary(),
            FindOnPath("codex.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "codex.cmd")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static string? FindDesktopAppBinary(string localAppData)
    {
        var binRoot = Path.Combine(localAppData, "OpenAI", "Codex", "bin");
        try
        {
            if (!Directory.Exists(binRoot)) return null;
            return Directory.EnumerateFiles(binRoot, "codex.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? FindRunningCodexBinary()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("codex"))
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            return path;
                    }
                    catch
                    {
                        // Some packaged processes do not expose MainModule. Continue with other locations.
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue)) return null;
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static QuotaSnapshot Error(string message) => new()
    {
        UpdatedAt = DateTimeOffset.Now,
        Source = "不可用",
        Error = message
    };
}
