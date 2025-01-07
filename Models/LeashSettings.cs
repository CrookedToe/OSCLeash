namespace VRCOSC.Modules.OSCLeash.Models;

public readonly struct LeashSettings
{
    public float WalkDeadzone { get; init; }
    public float RunDeadzone { get; init; }
    public float StrengthMultiplier { get; init; }
    public bool TurningEnabled { get; init; }
    public float TurningMultiplier { get; init; }
    public float TurningDeadzone { get; init; }
    public float TurningGoal { get; init; }
    public float SmoothTurningSpeed { get; init; }
    public float TurningMomentum { get; init; }
    public float UpDownCompensation { get; init; }
    public float UpDownDeadzone { get; init; }
    public bool EnableSafetyLimits { get; init; }
    public float MaxVelocity { get; init; }
    public float MaxAcceleration { get; init; }
    public float MaxTurnRate { get; init; }
    public string MovementCurveType { get; init; }
    public float CurveExponent { get; init; }
    public float CurveSmoothing { get; init; }
    public float InterpolationStrength { get; init; }
    public float StateTransitionTime { get; init; }

    public static LeashSettings FromSettings(OSCLeashModuleSettings settings)
    {
        return new LeashSettings
        {
            WalkDeadzone = settings.WalkDeadzone.Value,
            RunDeadzone = settings.RunDeadzone.Value,
            StrengthMultiplier = settings.StrengthMultiplier.Value,
            TurningEnabled = settings.TurningEnabled.Value,
            TurningMultiplier = settings.TurningMultiplier.Value,
            TurningDeadzone = settings.TurningDeadzone.Value,
            TurningGoal = settings.TurningGoal.Value,
            SmoothTurningSpeed = settings.SmoothTurningSpeed.Value,
            TurningMomentum = settings.TurningMomentum.Value,
            UpDownCompensation = settings.UpDownCompensation.Value,
            UpDownDeadzone = settings.UpDownDeadzone.Value,
            EnableSafetyLimits = settings.EnableSafetyLimits.Value,
            MaxVelocity = settings.MaxVelocity.Value,
            MaxAcceleration = settings.MaxAcceleration.Value,
            MaxTurnRate = settings.MaxTurnRate.Value,
            MovementCurveType = settings.MovementCurveType.Value,
            CurveExponent = settings.CurveExponent.Value,
            CurveSmoothing = settings.CurveSmoothing.Value,
            InterpolationStrength = settings.InterpolationStrength.Value,
            StateTransitionTime = settings.StateTransitionTime.Value
        };
    }
} 