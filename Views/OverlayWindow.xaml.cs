using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CECOMPuang.Helpers;
using CECOMPuang.Models;

namespace CECOMPuang.Views;

public partial class OverlayWindow : Window
{
    private const double PuangSize = 80;
    private const double ParticleLifetime = 0.8;
    private const double ParticleSize = 7;

    private const string IdleImage = "pack://application:,,,/Assets/Puang_idle.png";
    private const string ActiveImage = "pack://application:,,,/Assets/Puang_active.png";

    private readonly DispatcherTimer _particleTimer;
    private readonly DispatcherTimer _topmostTimer;

    private PuangVisual? _visual;
    private bool _visualAttached;

    public double DpiScaleX { get; private set; } = 1.0;
    public double DpiScaleY { get; private set; } = 1.0;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        _particleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _particleTimer.Tick += OnParticleTick;
        _particleTimer.Start();

        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _topmostTimer.Tick += (_, _) => ReassertTopmost();
        _topmostTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetClickThrough();
        ReassertTopmost();
    }

    private void ReassertTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public void CoverScreen(System.Windows.Forms.Screen screen)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        DpiScaleX = dpi.DpiScaleX;
        DpiScaleY = dpi.DpiScaleY;
        Left = screen.Bounds.Left / DpiScaleX;
        Top = screen.Bounds.Top / DpiScaleY;
        Width = screen.Bounds.Width / DpiScaleX;
        Height = screen.Bounds.Height / DpiScaleY;
    }

    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }

    public void ShowPuang(double x, double y, bool isActive, string name, bool showName,
        int comboCount, List<Particle>? particles, bool isSleeping)
    {
        EnsureVisual();
        var visual = _visual!;

        var imgPath = isActive ? ActiveImage : IdleImage;
        if (visual.CurrentImagePath != imgPath)
        {
            visual.PuangImage.Source = new BitmapImage(new Uri(imgPath));
            visual.CurrentImagePath = imgPath;
        }

        visual.NameLabel.Text = name;
        visual.NameBorder.Visibility = showName ? Visibility.Visible : Visibility.Collapsed;

        var left = x / DpiScaleX - Left;
        var top = y / DpiScaleY - Top;
        Canvas.SetLeft(visual.Container, left);
        Canvas.SetTop(visual.Container, top);

        // Use DesiredSize (after Measure) instead of ActualWidth — ActualWidth is 0
        // until the first layout pass completes, causing the name to flicker on first show.
        visual.NameBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var nameWidth = visual.NameBorder.DesiredSize.Width;
        Canvas.SetLeft(visual.NameBorder, left + (PuangSize - nameWidth) / 2);
        Canvas.SetTop(visual.NameBorder, top + PuangSize - 8);

        if (comboCount > 0)
        {
            visual.ComboLabel.Text = $"x{comboCount}";
            visual.ComboLabel.Foreground = new SolidColorBrush(GetComboColor(comboCount));
            visual.ComboLabel.Visibility = Visibility.Visible;
            Canvas.SetLeft(visual.ComboLabel, left + PuangSize - 4);
            Canvas.SetTop(visual.ComboLabel, top - 16);
        }
        else
        {
            visual.ComboLabel.Visibility = Visibility.Collapsed;
        }

        if (isSleeping)
        {
            visual.SleepLabel.Visibility = Visibility.Visible;
            Canvas.SetLeft(visual.SleepLabel, left + PuangSize - 4);
            Canvas.SetTop(visual.SleepLabel, top - 16);
        }
        else
        {
            visual.SleepLabel.Visibility = Visibility.Collapsed;
        }

        visual.Particles = particles;
        visual.PuangLeft = left;
        visual.PuangTop = top;
    }

    public void HidePuang()
    {
        if (_visual == null) return;
        _visual.Container.Visibility = Visibility.Collapsed;
        _visual.NameBorder.Visibility = Visibility.Collapsed;
        _visual.ComboLabel.Visibility = Visibility.Collapsed;
        _visual.SleepLabel.Visibility = Visibility.Collapsed;
        foreach (var el in _visual.ParticleElements)
            OverlayCanvas.Children.Remove(el);
        _visual.ParticleElements.Clear();
        _visual.Particles = null;
    }

    private void EnsureVisual()
    {
        if (_visual == null)
        {
            _visual = new PuangVisual();
            _visual.PuangImage.Cursor = Cursors.Hand;
        }

        if (!_visualAttached)
        {
            OverlayCanvas.Children.Add(_visual.Container);
            OverlayCanvas.Children.Add(_visual.NameBorder);
            OverlayCanvas.Children.Add(_visual.ComboLabel);
            OverlayCanvas.Children.Add(_visual.SleepLabel);
            _visualAttached = true;
        }

        _visual.Container.Visibility = Visibility.Visible;
    }

    private void OnParticleTick(object? sender, EventArgs e)
    {
        if (_visual == null) return;

        var now = DateTime.Now;
        var sleepPhase = now.TimeOfDay.TotalSeconds % 1.5 / 1.5;
        var sleepOpacity = 0.4 + 0.6 * (0.5 + 0.5 * Math.Sin(sleepPhase * 2 * Math.PI));

        if (_visual.SleepLabel.Visibility == Visibility.Visible)
            _visual.SleepLabel.Opacity = sleepOpacity;

        foreach (var el in _visual.ParticleElements)
            OverlayCanvas.Children.Remove(el);
        _visual.ParticleElements.Clear();

        if (_visual.Particles == null || _visual.Particles.Count == 0) return;

        _visual.Particles.RemoveAll(p => (now - p.Created).TotalSeconds > ParticleLifetime);

        var centerX = _visual.PuangLeft + PuangSize / 2;
        var bottomY = _visual.PuangTop + PuangSize * 0.75;

        foreach (var p in _visual.Particles)
        {
            var age = (now - p.Created).TotalSeconds;
            var progress = age / ParticleLifetime;
            var opacity = 1.0 - progress * progress;
            var size = ParticleSize * (1.0 - progress * 0.4);

            var px = centerX + p.StartX + p.Dx * progress;
            var py = bottomY + p.Dy * progress;
            var color = GetParticleColor(p.Color);

            AddParticleEllipse(px, py, size * 2.5, color, opacity * 0.35);
            AddParticleEllipse(px, py, size, color, opacity);
        }
    }

    private void AddParticleEllipse(double cx, double cy, double size, Color color, double opacity)
    {
        var el = new Ellipse
        {
            Width = size, Height = size,
            Fill = new SolidColorBrush(color) { Opacity = opacity },
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(el, cx - size / 2);
        Canvas.SetTop(el, cy - size / 2);
        OverlayCanvas.Children.Add(el);
        _visual!.ParticleElements.Add(el);
    }

    private static Color GetComboColor(int combo) => combo switch
    {
        >= 150 => Color.FromRgb(255, 77, 179),
        >= 100 => Colors.Red,
        >= 60 => Colors.Orange,
        >= 30 => Color.FromRgb(77, 255, 128),
        _ => Color.FromRgb(77, 230, 255),
    };

    private static Color GetParticleColor(string color) => color switch
    {
        "Red" => Color.FromRgb(255, 50, 50),
        "Orange" => Color.FromRgb(255, 153, 50),
        "Yellow" => Color.FromRgb(255, 242, 77),
        "Green" => Color.FromRgb(77, 255, 128),
        "Cyan" => Color.FromRgb(77, 230, 255),
        "Blue" => Color.FromRgb(102, 153, 255),
        "Pink" => Color.FromRgb(255, 77, 179),
        _ => Colors.White,
    };

    private class PuangVisual
    {
        public Canvas Container { get; } = new() { Width = PuangSize, Height = PuangSize };
        public Image PuangImage { get; }
        public string? CurrentImagePath { get; set; }
        public TextBlock NameLabel { get; }
        public Border NameBorder { get; }
        public TextBlock ComboLabel { get; }
        public TextBlock SleepLabel { get; }
        public List<UIElement> ParticleElements { get; } = new();
        public List<Particle>? Particles { get; set; }
        public double PuangLeft { get; set; }
        public double PuangTop { get; set; }

        public PuangVisual()
        {
            PuangImage = new Image { Width = PuangSize, Height = PuangSize };
            RenderOptions.SetBitmapScalingMode(PuangImage, BitmapScalingMode.NearestNeighbor);
            Container.Children.Add(PuangImage);

            NameLabel = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
            };

            NameBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Child = NameLabel,
            };

            ComboLabel = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            SleepLabel = new TextBlock
            {
                Text = "\U0001F4A4",
                FontSize = 18,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
        }
    }
}
