using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace CECOMPuang.Models;

public class Particle
{
    public double StartX { get; init; }
    public double Dx { get; init; }
    public double Dy { get; init; }
    public DateTime Created { get; } = DateTime.Now;
    public string Color { get; init; } = "Cyan";
}

public class CECOMPuangState : INotifyPropertyChanged
{
    private static readonly Random Rng = new();

    private static readonly string[][] ColorPools =
    {
        new[] { "Cyan", "Blue", "White" },
        new[] { "Green", "Cyan", "Yellow" },
        new[] { "Yellow", "Orange", "Pink" },
        new[] { "Orange", "Red", "Pink" },
        new[] { "Red", "Pink", "Orange" },
    };

    private double _absX;
    private double _absY;
    private bool _isActive;
    private string _name = "Anonymous";
    private bool _showName = true;
    private int _keystrokeCount;
    private long _totalKeystrokeCount;
    private int _comboCount;
    private bool _powerMode = true;
    private bool _isSleeping;
    private DispatcherTimer? _comboResetTimer;

    public Action? OnComboReset { get; set; }

    public double AbsX
    {
        get => _absX;
        set => SetField(ref _absX, value);
    }

    public double AbsY
    {
        get => _absY;
        set => SetField(ref _absY, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool ShowName
    {
        get => _showName;
        set => SetField(ref _showName, value);
    }

    public int KeystrokeCount
    {
        get => _keystrokeCount;
        set => SetField(ref _keystrokeCount, value);
    }

    public long TotalKeystrokeCount
    {
        get => _totalKeystrokeCount;
        set => SetField(ref _totalKeystrokeCount, value);
    }

    public int ComboCount
    {
        get => _comboCount;
        set => SetField(ref _comboCount, value);
    }

    public bool PowerMode
    {
        get => _powerMode;
        set => SetField(ref _powerMode, value);
    }

    public bool IsSleeping
    {
        get => _isSleeping;
        set => SetField(ref _isSleeping, value);
    }

    public List<Particle> Particles { get; } = new();

    public void IncrementKeystroke()
    {
        KeystrokeCount++;
        TotalKeystrokeCount++;
    }

    public void BumpCombo()
    {
        if (!PowerMode) return;

        ComboCount++;

        _comboResetTimer ??= CreateComboResetTimer();
        _comboResetTimer.Stop();
        _comboResetTimer.Start();

        SpawnParticles();
    }

    private DispatcherTimer CreateComboResetTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ComboCount = 0;
            Particles.Clear();
            OnComboReset?.Invoke();
        };
        return timer;
    }

    private void SpawnParticles()
    {
        var count = Math.Min(3 + ComboCount / 10, 8);
        var pool = PoolForCombo(ComboCount);
        for (var i = 0; i < count; i++)
        {
            Particles.Add(new Particle
            {
                StartX = Rng.NextDouble() * 60 - 30,
                Dx = Rng.NextDouble() * 30 - 15,
                Dy = Rng.NextDouble() * -35 - 15,
                Color = pool[Rng.Next(pool.Length)],
            });
        }
    }

    private static string[] PoolForCombo(int combo) => combo switch
    {
        >= 150 => ColorPools[4],
        >= 100 => ColorPools[3],
        >= 60 => ColorPools[2],
        >= 30 => ColorPools[1],
        _ => ColorPools[0],
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
