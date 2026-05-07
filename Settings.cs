using System.Configuration;

namespace CECOMPuang;

internal sealed class Settings : ApplicationSettingsBase
{
    private static readonly Settings _default = (Settings)Synchronized(new Settings());
    public static Settings Default => _default;

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string CECOMPuangName
    {
        get => (string)this[nameof(CECOMPuangName)];
        set => this[nameof(CECOMPuangName)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public double CECOMPuangX
    {
        get => (double)this[nameof(CECOMPuangX)];
        set => this[nameof(CECOMPuangX)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public double CECOMPuangY
    {
        get => (double)this[nameof(CECOMPuangY)];
        set => this[nameof(CECOMPuangY)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("True")]
    public bool ShowName
    {
        get => (bool)this[nameof(ShowName)];
        set => this[nameof(ShowName)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public int KeystrokeCount
    {
        get => (int)this[nameof(KeystrokeCount)];
        set => this[nameof(KeystrokeCount)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public long TotalKeystrokeCount
    {
        get => (long)this[nameof(TotalKeystrokeCount)];
        set => this[nameof(TotalKeystrokeCount)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string KeystrokeDate
    {
        get => (string)this[nameof(KeystrokeDate)];
        set => this[nameof(KeystrokeDate)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("True")]
    public bool PowerMode
    {
        get => (bool)this[nameof(PowerMode)];
        set => this[nameof(PowerMode)] = value;
    }
}
