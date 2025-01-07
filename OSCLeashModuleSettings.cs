using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using VRCOSC.Modules.OSCLeash.UI;

namespace VRCOSC.Modules.OSCLeash;

public class OSCLeashModuleSettings : StringModuleSetting
{
    // Base variable name and direction
    [JsonProperty("leash_variable")]
    public Observable<string> LeashVariable { get; set; } = new("Leash");

    [JsonProperty("leash_direction")]
    public Observable<string> LeashDirection { get; set; } = new("North");

    // Core Settings
    [JsonProperty("turning_enabled")]
    public Observable<bool> TurningEnabled { get; set; } = new(true);
    
    [JsonProperty("turning_multiplier")]
    public Observable<float> TurningMultiplier { get; set; } = new(1.0f);
    
    [JsonProperty("turning_deadzone")]
    public Observable<float> TurningDeadzone { get; set; } = new(0.1f);
    
    [JsonProperty("turning_goal")]
    public Observable<float> TurningGoal { get; set; } = new(90.0f);
    
    [JsonProperty("smooth_turning_speed")]
    public Observable<float> SmoothTurningSpeed { get; set; } = new(0.5f);
    
    [JsonProperty("turning_momentum")]
    public Observable<float> TurningMomentum { get; set; } = new(0.3f);

    [JsonProperty("walk_deadzone")]
    public Observable<float> WalkDeadzone { get; set; } = new(0.1f);
    
    [JsonProperty("run_deadzone")]
    public Observable<float> RunDeadzone { get; set; } = new(0.8f);
    
    [JsonProperty("strength_multiplier")]
    public Observable<float> StrengthMultiplier { get; set; } = new(1.0f);

    // Vertical Movement Settings
    [JsonProperty("up_down_compensation")]
    public Observable<float> UpDownCompensation { get; set; } = new(1.0f);
    
    [JsonProperty("up_down_deadzone")]
    public Observable<float> UpDownDeadzone { get; set; } = new(0.1f);

    // Safety Settings
    [JsonProperty("enable_safety_limits")]
    public Observable<bool> EnableSafetyLimits { get; set; } = new(true);
    
    [JsonProperty("max_velocity")]
    public Observable<float> MaxVelocity { get; set; } = new(1.0f);
    
    [JsonProperty("max_acceleration")]
    public Observable<float> MaxAcceleration { get; set; } = new(2.0f);
    
    [JsonProperty("max_turn_rate")]
    public Observable<float> MaxTurnRate { get; set; } = new(180.0f);

    // Movement Enhancement Settings
    [JsonProperty("interpolation_strength")]
    public Observable<float> InterpolationStrength { get; set; } = new(0.5f);
    
    [JsonProperty("state_transition_time")]
    public Observable<float> StateTransitionTime { get; set; } = new(0.2f);
    
    [JsonProperty("movement_curve_type")]
    public Observable<string> MovementCurveType { get; set; } = new("Linear");
    
    [JsonProperty("curve_exponent")]
    public Observable<float> CurveExponent { get; set; } = new(2.0f);
    
    [JsonProperty("curve_smoothing")]
    public Observable<float> CurveSmoothing { get; set; } = new(0.5f);

    public OSCLeashModuleSettings()
        : base("Settings", "Configure leash behavior", typeof(LeashModuleSettingView), string.Empty)
    {
    }
} 