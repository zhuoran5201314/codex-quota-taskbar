using System.Threading;
using System.Windows;

namespace CodexQuotaDashboard;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private DashboardController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(true, "Local\\CodexQuotaDashboard.SCZ", out var created);
        if (!created)
        {
            MessageBox.Show("Codex 额度仪表盘已经在运行。", "Codex 额度仪表盘",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _controller = new DashboardController();
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
