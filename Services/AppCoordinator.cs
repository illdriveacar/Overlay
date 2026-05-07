using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CECOMPuang.Models;
using CECOMPuang.Views;
using Hardcodet.Wpf.TaskbarNotification;

namespace CECOMPuang.Services;

public sealed class AppCoordinator : IDisposable
{
    private const double PuangSize = 80;
    private const double IdleSleepMs = 30_000;
    private const double DragClickThreshold = 5;

    private readonly CECOMPuangState _puang;
    private readonly GlobalInputMonitor _inputMonitor = new();
    private readonly List<OverlayWindow> _overlays = new();

    private TaskbarIcon? _trayIcon;
    private MenuItem? _keystrokeMenuItem;
    private MenuItem? _totalKeystrokeMenuItem;
    private MenuItem? _nameMenuItem;

    private DispatcherTimer? _activityTimer;
    private DateTime _lastActivity = DateTime.MinValue;
    private bool _disposed;

    private bool _isDragging;
    private double _dragStartX, _dragStartY;
    private double _dragOffsetX, _dragOffsetY;

    public AppCoordinator()
    {
        var (initX, initY) = ResolveInitialPosition();

        _puang = new CECOMPuangState
        {
            Name = Settings.Default.CECOMPuangName.Length > 0 ? Settings.Default.CECOMPuangName : "Anonymous",
            AbsX = initX,
            AbsY = initY,
            ShowName = Settings.Default.ShowName,
            KeystrokeCount = LoadKeystrokeCount(),
            TotalKeystrokeCount = Settings.Default.TotalKeystrokeCount,
            PowerMode = Settings.Default.PowerMode,
        };
    }

    public void Start()
    {
        _puang.OnComboReset = RefreshOverlays;
        SetupOverlays();
        SetupTrayIcon();
        SetupInputMonitor();
        SetupActivityTimer();
    }

    // ── setup ────────────────────────────────────────────────────

    private void SetupOverlays()
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var overlay = new OverlayWindow();
            overlay.Show();
            overlay.CoverScreen(screen);
            _overlays.Add(overlay);
        }
        RefreshOverlays();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "CECOMPuang",
            Icon = LoadAppIcon(),
            MenuActivation = PopupActivationMode.LeftOrRightClick,
        };

        _keystrokeMenuItem = new MenuItem
        {
            Header = $"Today: {_puang.KeystrokeCount:N0} keystrokes",
            IsEnabled = false,
        };
        _totalKeystrokeMenuItem = new MenuItem
        {
            Header = $"Total: {_puang.TotalKeystrokeCount:N0} keystrokes",
            IsEnabled = false,
        };
        _nameMenuItem = new MenuItem { Header = $"Name: {_puang.Name}", IsEnabled = false };

        var renameItem = new MenuItem { Header = "Rename..." };
        renameItem.Click += (_, _) => ShowRenameDialog();

        var showNameItem = new MenuItem
        {
            Header = "Show Name",
            IsCheckable = true,
            IsChecked = _puang.ShowName,
        };
        showNameItem.Click += (_, _) =>
        {
            _puang.ShowName = showNameItem.IsChecked;
            Settings.Default.ShowName = showNameItem.IsChecked;
            Settings.Default.Save();
            RefreshOverlays();
        };

        var powerModeItem = new MenuItem
        {
            Header = "Power Mode",
            IsCheckable = true,
            IsChecked = _puang.PowerMode,
        };
        powerModeItem.Click += (_, _) =>
        {
            _puang.PowerMode = powerModeItem.IsChecked;
            Settings.Default.PowerMode = powerModeItem.IsChecked;
            Settings.Default.Save();
            if (!_puang.PowerMode)
            {
                _puang.ComboCount = 0;
                _puang.Particles.Clear();
                RefreshOverlays();
            }
        };

        var startupItem = new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = StartupRegistry.IsEnabled(),
        };
        startupItem.Click += (_, _) =>
        {
            if (startupItem.IsChecked)
                StartupRegistry.Enable();
            else
                StartupRegistry.Disable();
        };

        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) =>
        {
            Dispose();
            Application.Current.Shutdown();
        };

        var menu = new ContextMenu();
        menu.Items.Add(_keystrokeMenuItem);
        menu.Items.Add(_totalKeystrokeMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_nameMenuItem);
        menu.Items.Add(renameItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(showNameItem);
        menu.Items.Add(powerModeItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);

        _trayIcon.ContextMenu = menu;
    }

    private void SetupInputMonitor()
    {
        _inputMonitor.OnActivity += () =>
            Application.Current.Dispatcher.Invoke(HandleActivity);
        _inputMonitor.OnLeftDown += (px, py) =>
            Application.Current.Dispatcher.Invoke(() => HandleGlobalLeftDown(px, py));
        _inputMonitor.OnMouseMove += (px, py) =>
        {
            if (_isDragging)
                Application.Current.Dispatcher.Invoke(() => HandleGlobalMouseMove(px, py));
        };
        _inputMonitor.OnLeftClick += (px, py) =>
            Application.Current.Dispatcher.Invoke(() => HandleGlobalLeftUp(px, py));
        _inputMonitor.Install();
    }

    private void SetupActivityTimer()
    {
        _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _activityTimer.Tick += (_, _) =>
        {
            var idleMs = (DateTime.Now - _lastActivity).TotalMilliseconds;
            if (!_puang.IsSleeping && _lastActivity != DateTime.MinValue && idleMs > IdleSleepMs)
            {
                _puang.IsSleeping = true;
                RefreshOverlays();
            }
            SaveKeystrokeCount();
        };
        _activityTimer.Start();
    }

    // ── input handling ───────────────────────────────────────────

    private void HandleActivity()
    {
        _lastActivity = DateTime.Now;
        _puang.IsSleeping = false;
        _puang.IncrementKeystroke();
        _puang.BumpCombo();
        _puang.IsActive = !_puang.IsActive;
        RefreshOverlays();
    }

    private bool HitTestPuang(int px, int py)
    {
        var scale = _overlays.FirstOrDefault()?.DpiScaleX ?? 1.0;
        var hitSize = PuangSize * Math.Max(scale, 1.0);
        var dx = px - _puang.AbsX;
        var dy = py - _puang.AbsY;
        return dx >= 0 && dx <= hitSize && dy >= 0 && dy <= hitSize;
    }

    private void HandleGlobalLeftDown(int px, int py)
    {
        if (!HitTestPuang(px, py)) return;

        _isDragging = true;
        _dragStartX = px;
        _dragStartY = py;
        _dragOffsetX = px - _puang.AbsX;
        _dragOffsetY = py - _puang.AbsY;
    }

    private void HandleGlobalMouseMove(int px, int py)
    {
        if (!_isDragging) return;

        var (newX, newY) = ClampToScreen(px - _dragOffsetX, py - _dragOffsetY);
        _puang.AbsX = newX;
        _puang.AbsY = newY;
        RefreshOverlays();
    }

    private void HandleGlobalLeftUp(int px, int py)
    {
        if (!_isDragging) return;

        var movedDist = Math.Sqrt(
            Math.Pow(px - _dragStartX, 2) + Math.Pow(py - _dragStartY, 2));
        if (movedDist >= DragClickThreshold)
            SavePosition();

        _isDragging = false;
    }

    // ── rendering ────────────────────────────────────────────────

    private void RefreshOverlays()
    {
        var puangPoint = new System.Drawing.Point((int)_puang.AbsX, (int)_puang.AbsY);
        var puangScreen = System.Windows.Forms.Screen.FromPoint(puangPoint);

        foreach (var overlay in _overlays)
        {
            var screen = GetOverlayScreen(overlay);
            if (screen?.DeviceName == puangScreen.DeviceName)
            {
                overlay.ShowPuang(
                    _puang.AbsX, _puang.AbsY, _puang.IsActive,
                    _puang.Name, _puang.ShowName,
                    _puang.ComboCount, _puang.Particles, _puang.IsSleeping);
            }
            else
            {
                overlay.HidePuang();
            }
        }
    }

    // ── tray actions ─────────────────────────────────────────────

    private void ShowRenameDialog()
    {
        var input = InputDialog.Show("Enter your name:", "Rename", _puang.Name);
        if (string.IsNullOrWhiteSpace(input)) return;

        var trimmed = input.Trim();
        var name = trimmed[..Math.Min(trimmed.Length, 50)];
        _puang.Name = name;
        Settings.Default.CECOMPuangName = name;
        Settings.Default.Save();

        if (_nameMenuItem != null)
            _nameMenuItem.Header = $"Name: {name}";

        RefreshOverlays();
    }

    // ── settings persistence ─────────────────────────────────────

    private void SavePosition()
    {
        Settings.Default.CECOMPuangX = _puang.AbsX;
        Settings.Default.CECOMPuangY = _puang.AbsY;
        Settings.Default.Save();
    }

    private static int LoadKeystrokeCount()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (Settings.Default.KeystrokeDate == today)
            return Settings.Default.KeystrokeCount;

        Settings.Default.KeystrokeCount = 0;
        Settings.Default.KeystrokeDate = today;
        Settings.Default.Save();
        return 0;
    }

    private void SaveKeystrokeCount()
    {
        Settings.Default.KeystrokeCount = _puang.KeystrokeCount;
        Settings.Default.KeystrokeDate = DateTime.Now.ToString("yyyy-MM-dd");
        Settings.Default.TotalKeystrokeCount = _puang.TotalKeystrokeCount;
        Settings.Default.Save();
        if (_keystrokeMenuItem != null)
            _keystrokeMenuItem.Header = $"Today: {_puang.KeystrokeCount:N0} keystrokes";
        if (_totalKeystrokeMenuItem != null)
            _totalKeystrokeMenuItem.Header = $"Total: {_puang.TotalKeystrokeCount:N0} keystrokes";
    }

    // ── helpers ──────────────────────────────────────────────────

    private static (double X, double Y) ResolveInitialPosition()
    {
        var x = Settings.Default.CECOMPuangX > 0 ? Settings.Default.CECOMPuangX : GetDefaultX();
        var y = Settings.Default.CECOMPuangY > 0 ? Settings.Default.CECOMPuangY : GetDefaultY();
        return ClampToScreen(x, y);
    }

    private static (double X, double Y) ClampToScreen(double x, double y)
    {
        var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
        const double margin = 40;
        return (
            Math.Clamp(x, vs.Left + margin, vs.Right - margin),
            Math.Clamp(y, vs.Top + margin, vs.Bottom - margin));
    }

    private static double GetDefaultX() =>
        System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Right - 120;

    private static double GetDefaultY() =>
        System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Bottom - 120;

    private static System.Windows.Forms.Screen? GetOverlayScreen(OverlayWindow overlay) =>
        System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)(overlay.Left + overlay.Width / 2),
                                     (int)(overlay.Top + overlay.Height / 2)));

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app_icon.png");
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream != null)
            {
                var bitmap = new System.Drawing.Bitmap(stream);
                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _activityTimer?.Stop();
        _inputMonitor.Dispose();
        _trayIcon?.Dispose();

        foreach (var overlay in _overlays)
            overlay.Close();
    }
}
