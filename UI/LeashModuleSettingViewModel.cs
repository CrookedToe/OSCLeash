using System.ComponentModel;
using System.Runtime.CompilerServices;
using VRCOSC.App.Utils;

namespace VRCOSC.Modules.OSCLeash.UI;

public class LeashModuleSettingViewModel : INotifyPropertyChanged
{
    private readonly OSCLeashModuleSettings _settings;

    public LeashModuleSettingViewModel(OSCLeashModuleSettings settings)
    {
        _settings = settings;
    }

    // Leash Configuration
    public string LeashVariable
    {
        get => _settings.LeashVariable.Value;
        set
        {
            if (_settings.LeashVariable.Value != value)
            {
                _settings.LeashVariable.Value = value;
                OnPropertyChanged();
            }
        }
    }

    public string[] DirectionOptions => new[] { "North", "South", "East", "West" };
    public string LeashDirection
    {
        get => _settings.LeashDirection.Value;
        set
        {
            if (_settings.LeashDirection.Value != value)
            {
                _settings.LeashDirection.Value = value;
                OnPropertyChanged();
            }
        }
    }

    // Movement Settings
    public float WalkDeadzone
    {
        get => _settings.WalkDeadzone.Value;
        set => UpdateValue(_settings.WalkDeadzone, value, 0f, 1f);
    }

    public float RunDeadzone
    {
        get => _settings.RunDeadzone.Value;
        set => UpdateValue(_settings.RunDeadzone, value, 0f, 1f);
    }

    public float StrengthMultiplier
    {
        get => _settings.StrengthMultiplier.Value;
        set => UpdateValue(_settings.StrengthMultiplier, value, 0f, 2f);
    }

    // Turning Settings
    public bool TurningEnabled
    {
        get => _settings.TurningEnabled.Value;
        set
        {
            if (_settings.TurningEnabled.Value != value)
            {
                _settings.TurningEnabled.Value = value;
                OnPropertyChanged();
            }
        }
    }

    public float TurningMultiplier
    {
        get => _settings.TurningMultiplier.Value;
        set => UpdateValue(_settings.TurningMultiplier, value, 0f, 2f);
    }

    public float TurningDeadzone
    {
        get => _settings.TurningDeadzone.Value;
        set => UpdateValue(_settings.TurningDeadzone, value, 0f, 1f);
    }

    public float TurningGoal
    {
        get => _settings.TurningGoal.Value;
        set => UpdateValue(_settings.TurningGoal, value, 0f, 180f);
    }

    public float SmoothTurningSpeed
    {
        get => _settings.SmoothTurningSpeed.Value;
        set => UpdateValue(_settings.SmoothTurningSpeed, value, 0f, 1f);
    }

    public float TurningMomentum
    {
        get => _settings.TurningMomentum.Value;
        set => UpdateValue(_settings.TurningMomentum, value, 0f, 1f);
    }

    // Vertical Movement Settings
    public float UpDownCompensation
    {
        get => _settings.UpDownCompensation.Value;
        set => UpdateValue(_settings.UpDownCompensation, value, 0f, 2f);
    }

    public float UpDownDeadzone
    {
        get => _settings.UpDownDeadzone.Value;
        set => UpdateValue(_settings.UpDownDeadzone, value, 0f, 1f);
    }

    // Safety Settings
    public bool EnableSafetyLimits
    {
        get => _settings.EnableSafetyLimits.Value;
        set
        {
            if (_settings.EnableSafetyLimits.Value != value)
            {
                _settings.EnableSafetyLimits.Value = value;
                OnPropertyChanged();
            }
        }
    }

    public float MaxVelocity
    {
        get => _settings.MaxVelocity.Value;
        set => UpdateValue(_settings.MaxVelocity, value, 0f, 2f);
    }

    public float MaxAcceleration
    {
        get => _settings.MaxAcceleration.Value;
        set => UpdateValue(_settings.MaxAcceleration, value, 0f, 5f);
    }

    public float MaxTurnRate
    {
        get => _settings.MaxTurnRate.Value;
        set => UpdateValue(_settings.MaxTurnRate, value, 0f, 360f);
    }

    // Movement Enhancements
    public string[] CurveTypeOptions => new[] { "Linear", "Quadratic", "Cubic", "Exponential" };
    public string MovementCurveType
    {
        get => _settings.MovementCurveType.Value;
        set
        {
            if (_settings.MovementCurveType.Value != value)
            {
                _settings.MovementCurveType.Value = value;
                OnPropertyChanged();
            }
        }
    }

    public float CurveExponent
    {
        get => _settings.CurveExponent.Value;
        set => UpdateValue(_settings.CurveExponent, value, 1f, 5f);
    }

    public float CurveSmoothing
    {
        get => _settings.CurveSmoothing.Value;
        set => UpdateValue(_settings.CurveSmoothing, value, 0f, 1f);
    }

    public float InterpolationStrength
    {
        get => _settings.InterpolationStrength.Value;
        set => UpdateValue(_settings.InterpolationStrength, value, 0f, 1f);
    }

    public float StateTransitionTime
    {
        get => _settings.StateTransitionTime.Value;
        set => UpdateValue(_settings.StateTransitionTime, value, 0f, 1f);
    }

    // Debug Settings
    public bool EnableDebugLogging
    {
        get => _settings.EnableDebugLogging.Value;
        set
        {
            if (_settings.EnableDebugLogging.Value != value)
            {
                _settings.EnableDebugLogging.Value = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateValue<T>(Observable<T> observable, T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0)
            value = min;
        else if (value.CompareTo(max) > 0)
            value = max;

        if (!observable.Value.Equals(value))
        {
            observable.Value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
} 