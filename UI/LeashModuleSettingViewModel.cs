using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;

namespace VRCOSC.Modules.OSCLeash.UI;

public class LeashModuleSettingViewModel
{
    private readonly OSCLeashModuleSettings _settings;

    public LeashModuleSettingViewModel(OSCLeashModuleSettings settings)
    {
        _settings = settings;
    }

    // Core Settings
    public string LeashVariable
    {
        get => _settings.LeashVariable.Value;
        set => _settings.LeashVariable.Value = value;
    }

    public string LeashDirection
    {
        get => _settings.LeashDirection.Value;
        set => _settings.LeashDirection.Value = value;
    }

    // Movement Settings
    public float WalkDeadzone
    {
        get => _settings.WalkDeadzone.Value;
        set => _settings.WalkDeadzone.Value = value;
    }

    public float RunDeadzone
    {
        get => _settings.RunDeadzone.Value;
        set => _settings.RunDeadzone.Value = value;
    }

    public float StrengthMultiplier
    {
        get => _settings.StrengthMultiplier.Value;
        set => _settings.StrengthMultiplier.Value = value;
    }

    public bool EnableSafetyLimits
    {
        get => _settings.EnableSafetyLimits.Value;
        set => _settings.EnableSafetyLimits.Value = value;
    }

    public float MaxVelocity
    {
        get => _settings.MaxVelocity.Value;
        set => _settings.MaxVelocity.Value = value;
    }

    // Turning Settings
    public bool TurningEnabled
    {
        get => _settings.TurningEnabled.Value;
        set => _settings.TurningEnabled.Value = value;
    }

    public float TurningMultiplier
    {
        get => _settings.TurningMultiplier.Value;
        set => _settings.TurningMultiplier.Value = value;
    }

    public float TurningDeadzone
    {
        get => _settings.TurningDeadzone.Value;
        set => _settings.TurningDeadzone.Value = value;
    }

    public float SmoothTurningSpeed
    {
        get => _settings.SmoothTurningSpeed.Value;
        set => _settings.SmoothTurningSpeed.Value = value;
    }

    public string[] DirectionOptions => new[] { "North", "South", "East", "West" };
} 