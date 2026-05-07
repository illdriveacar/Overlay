using System.Threading;
using System.Windows;
using CECOMPuang.Services;

namespace CECOMPuang;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\CECOMPuang_SingleInstance_{3f8c2a1d-9b4e-4a7d-8c1f-2e5b7a9d0c3e}";

    private Mutex? _singleInstanceMutex;
    private AppCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        _coordinator = new AppCoordinator();
        _coordinator.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();

        if (_singleInstanceMutex != null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}
