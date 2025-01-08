using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using VRCOSC.Modules.OSCLeash.UI;

namespace VRCOSC.Modules.OSCLeash;

public class OSCLeashModuleSettings : StringModuleSetting
{
    // Core Settings
    [JsonProperty("leash_variable")]
    public Observable<string> LeashVariable { get; set; } = new("Leash");

    [JsonProperty("leash_direction")]
    public Observable<string> LeashDirection { get; set; } = new("North");

    // Movement Settings
    [JsonProperty("walk_deadzone")]
    public Observable<float> WalkDeadzone { get; set; } = new(0.15f);

    [JsonProperty("run_deadzone")]
    public Observable<float> RunDeadzone { get; set; } = new(0.70f);

    [JsonProperty("strength_multiplier")]
    public Observable<float> StrengthMultiplier { get; set; } = new(1.2f);

    [JsonProperty("enable_safety_limits")]
    public Observable<bool> EnableSafetyLimits { get; set; } = new(true);

    [JsonProperty("max_velocity")]
    public Observable<float> MaxVelocity { get; set; } = new(1.0f);

    // Turning Settings
    [JsonProperty("turning_enabled")]
    public Observable<bool> TurningEnabled { get; set; } = new(false);

    [JsonProperty("turning_multiplier")]
    public Observable<float> TurningMultiplier { get; set; } = new(0.8f);

    [JsonProperty("turning_deadzone")]
    public Observable<float> TurningDeadzone { get; set; } = new(0.15f);

    [JsonProperty("smooth_turning_speed")]
    public Observable<float> SmoothTurningSpeed { get; set; } = new(1.0f);

    public OSCLeashModuleSettings()
        : base("Settings", "Configure leash behavior", typeof(LeashModuleSettingView), "Settings")
    {
    }
} 