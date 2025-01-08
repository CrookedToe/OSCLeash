using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using VRCOSC.Modules.OSCLeash.UI;

namespace VRCOSC.Modules.OSCLeash;

public class OSCLeashModuleSettings : StringModuleSetting
{
    private static readonly string[] ValidDirections = { "North", "South", "East", "West" };

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

    [JsonProperty("state_transition_time")]
    public Observable<float> StateTransitionTime { get; set; } = new(0.2f);

    public OSCLeashModuleSettings()
        : base("Settings", "Configure leash behavior", typeof(LeashModuleSettingView), "Settings")
    {
        // Subscribe to settings changes for validation
        LeashDirection.Subscribe(OnDirectionChanged);
        WalkDeadzone.Subscribe(OnWalkDeadzoneChanged);
        RunDeadzone.Subscribe(OnRunDeadzoneChanged);
        MaxVelocity.Subscribe(OnMaxVelocityChanged);
        TurningEnabled.Subscribe(OnTurningEnabledChanged);
        TurningMultiplier.Subscribe(OnTurningMultiplierChanged);
        TurningDeadzone.Subscribe(OnTurningDeadzoneChanged);
        SmoothTurningSpeed.Subscribe(OnSmoothTurningSpeedChanged);
        StateTransitionTime.Subscribe(OnStateTransitionTimeChanged);
    }

    private void OnDirectionChanged(string newDirection)
    {
        if (!ValidDirections.Contains(newDirection, StringComparer.OrdinalIgnoreCase))
        {
            LeashDirection.Value = "North"; // Reset to default if invalid
        }
    }

    private void OnWalkDeadzoneChanged(float newValue)
    {
        // Clamp between 0 and run deadzone
        WalkDeadzone.Value = Math.Clamp(newValue, 0f, RunDeadzone.Value);
    }

    private void OnRunDeadzoneChanged(float newValue)
    {
        // Clamp between walk deadzone and 1
        RunDeadzone.Value = Math.Clamp(newValue, WalkDeadzone.Value, 1f);
    }

    private void OnMaxVelocityChanged(float newValue)
    {
        // Ensure positive value
        if (newValue <= 0f)
        {
            MaxVelocity.Value = 1.0f;
        }
    }

    private void OnTurningEnabledChanged(bool enabled)
    {
        if (!enabled)
        {
            // Reset turning values when disabled
            TurningMultiplier.Value = 0.8f;
            TurningDeadzone.Value = 0.15f;
            SmoothTurningSpeed.Value = 1.0f;
        }
    }

    private void OnTurningMultiplierChanged(float newValue)
    {
        if (!TurningEnabled.Value)
        {
            TurningMultiplier.Value = 0.8f;
            return;
        }

        // Ensure positive value
        TurningMultiplier.Value = Math.Abs(newValue);
    }

    private void OnTurningDeadzoneChanged(float newValue)
    {
        if (!TurningEnabled.Value)
        {
            TurningDeadzone.Value = 0.15f;
            return;
        }

        // Clamp between 0 and 1
        TurningDeadzone.Value = Math.Clamp(newValue, 0f, 1f);
    }

    private void OnSmoothTurningSpeedChanged(float newValue)
    {
        if (!TurningEnabled.Value)
        {
            SmoothTurningSpeed.Value = 1.0f;
            return;
        }

        // Ensure positive value
        SmoothTurningSpeed.Value = Math.Max(0.1f, newValue);
    }

    private void OnStateTransitionTimeChanged(float newValue)
    {
        // Ensure non-negative value with reasonable minimum
        StateTransitionTime.Value = Math.Max(0.016f, newValue);
    }
} 