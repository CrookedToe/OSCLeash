using System;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace VRCOSC.Modules.OSCLeash;

[ModuleTitle("OSC Leash")]
[ModuleDescription("Allows for controlling avatar movement with parameters")]
[ModuleType(ModuleType.Generic)]
[ModulePrefab("OSCLeash", "https://github.com/CrookedToe/OSCLeash/tree/main/Unity")]
[ModuleInfo("https://github.com/CrookedToe/OSCLeash")]
public class OSCLeashModule : Module
{
    private bool isGrabbed;
    private float stretch;
    private float zPositive;
    private float zNegative;
    private float xPositive;
    private float xNegative;
    private float yPositive;
    private float yNegative;

    private const string PARAM_PREFIX = "avatar/parameters/";
    private const string INPUT_PREFIX = "input/";

    private enum OSCLeashParameter
    {
        Stretch,
        IsGrabbed,
        ZPositive,
        ZNegative,
        XPositive,
        XNegative,
        YPositive,
        YNegative,
        // Output parameters
        Vertical,
        Horizontal,
        Run
    }

    private enum OSCLeashSetting
    {
        RunDeadzone,
        WalkDeadzone,
        StrengthMultiplier,
        UpDownCompensation,
        UpDownDeadzone
    }

    protected override void OnPreLoad()
    {
        // Create settings
        CreateSlider(OSCLeashSetting.RunDeadzone, "Run Deadzone", "Stretch threshold for running", 0.70f, 0.0f, 1.0f);
        CreateSlider(OSCLeashSetting.WalkDeadzone, "Walk Deadzone", "Stretch threshold for walking", 0.15f, 0.0f, 1.0f);
        CreateSlider(OSCLeashSetting.StrengthMultiplier, "Strength Multiplier", "Movement strength multiplier", 1.2f, 0.1f, 5.0f);
        CreateSlider(OSCLeashSetting.UpDownCompensation, "Up/Down Compensation", "Compensation for vertical movement", 1.0f, 0.0f, 2.0f);
        CreateSlider(OSCLeashSetting.UpDownDeadzone, "Up/Down Deadzone", "Vertical angle deadzone", 0.5f, 0.0f, 1.0f);

        // Register parameters we want to receive
        RegisterParameter<float>(OSCLeashParameter.Stretch, $"{PARAM_PREFIX}Leash_Stretch", ParameterMode.Read, "Leash Stretch", "Physbone stretch value");
        RegisterParameter<bool>(OSCLeashParameter.IsGrabbed, $"{PARAM_PREFIX}Leash_IsGrabbed", ParameterMode.Read, "Leash Grabbed", "Physbone grab state");
        
        // Direction parameters
        RegisterParameter<float>(OSCLeashParameter.ZPositive, $"{PARAM_PREFIX}Leash_ZPositive", ParameterMode.Read, "Forward Direction", "Forward movement value");
        RegisterParameter<float>(OSCLeashParameter.ZNegative, $"{PARAM_PREFIX}Leash_ZNegative", ParameterMode.Read, "Backward Direction", "Backward movement value");
        RegisterParameter<float>(OSCLeashParameter.XPositive, $"{PARAM_PREFIX}Leash_XPositive", ParameterMode.Read, "Right Direction", "Right movement value");
        RegisterParameter<float>(OSCLeashParameter.XNegative, $"{PARAM_PREFIX}Leash_XNegative", ParameterMode.Read, "Left Direction", "Left movement value");
        RegisterParameter<float>(OSCLeashParameter.YPositive, $"{PARAM_PREFIX}Leash_YPositive", ParameterMode.Read, "Up Direction", "Upward movement value");
        RegisterParameter<float>(OSCLeashParameter.YNegative, $"{PARAM_PREFIX}Leash_YNegative", ParameterMode.Read, "Down Direction", "Downward movement value");

        // Register output parameters
        RegisterParameter<float>(OSCLeashParameter.Vertical, $"{INPUT_PREFIX}Vertical", ParameterMode.Write, "Vertical Movement", "Forward/backward movement (-1 to 1)");
        RegisterParameter<float>(OSCLeashParameter.Horizontal, $"{INPUT_PREFIX}Horizontal", ParameterMode.Write, "Horizontal Movement", "Left/right movement (-1 to 1)");
        RegisterParameter<int>(OSCLeashParameter.Run, $"{INPUT_PREFIX}Run", ParameterMode.Write, "Run State", "Whether to run (1) or walk (0)");

        // Create settings groups
        CreateGroup("Movement", OSCLeashSetting.RunDeadzone, OSCLeashSetting.WalkDeadzone, OSCLeashSetting.StrengthMultiplier);
        CreateGroup("Up/Down", OSCLeashSetting.UpDownCompensation, OSCLeashSetting.UpDownDeadzone);
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case OSCLeashParameter.Stretch:
                stretch = parameter.GetValue<float>();
                Log($"Stretch: {stretch}");
                break;
            case OSCLeashParameter.IsGrabbed:
                var newGrabState = parameter.GetValue<bool>();
                Log($"Grab state changed: {isGrabbed} -> {newGrabState}");
                isGrabbed = newGrabState;
                break;
            case OSCLeashParameter.ZPositive:
                zPositive = parameter.GetValue<float>();
                Log($"Z+: {zPositive}");
                break;
            case OSCLeashParameter.ZNegative:
                zNegative = parameter.GetValue<float>();
                Log($"Z-: {zNegative}");
                break;
            case OSCLeashParameter.XPositive:
                xPositive = parameter.GetValue<float>();
                Log($"X+: {xPositive}");
                break;
            case OSCLeashParameter.XNegative:
                xNegative = parameter.GetValue<float>();
                Log($"X-: {xNegative}");
                break;
            case OSCLeashParameter.YPositive:
                yPositive = parameter.GetValue<float>();
                Log($"Y+: {yPositive}");
                break;
            case OSCLeashParameter.YNegative:
                yNegative = parameter.GetValue<float>();
                Log($"Y-: {yNegative}");
                break;
        }
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 20)] // 50Hz update rate
    private void UpdateMovement()
    {
        var player = GetPlayer();
        if (player == null)
        {
            return;
        }

        if (!isGrabbed)
        {
            player.StopRun();
            player.MoveVertical(0);
            player.MoveHorizontal(0);
            return;
        }

        // Movement Math
        var strengthMultiplier = GetSettingValue<float>(OSCLeashSetting.StrengthMultiplier);
        var outputMultiplier = stretch * strengthMultiplier;

        var verticalOutput = Clamp((zPositive - zNegative) * outputMultiplier);
        var horizontalOutput = Clamp((xPositive - xNegative) * outputMultiplier);

        // Up/Down compensation
        var yCombined = yPositive + yNegative;
        var upDownDeadzone = GetSettingValue<float>(OSCLeashSetting.UpDownDeadzone);

        if (yCombined >= upDownDeadzone)
        {
            player.StopRun();
            player.MoveVertical(0);
            player.MoveHorizontal(0);
            return;
        }

        var upDownCompensation = GetSettingValue<float>(OSCLeashSetting.UpDownCompensation);
        if (upDownCompensation != 0)
        {
            var yModifier = Clamp(1.0f - (yCombined * upDownCompensation));
            if (yModifier != 0.0f)
            {
                verticalOutput /= yModifier;
                horizontalOutput /= yModifier;
            }
        }

        // Apply movement based on stretch
        var runDeadzone = GetSettingValue<float>(OSCLeashSetting.RunDeadzone);
        var isRunning = stretch > runDeadzone;

        // Send run state first to ensure it's applied before movement
        if (isRunning)
        {
            player.Run();
            // Send movement after a small delay to ensure run state is applied
            Task.Delay(16).ContinueWith(_ =>
            {
                player.MoveVertical(verticalOutput);
                player.MoveHorizontal(horizontalOutput);
            });
        }
        else
        {
            player.StopRun();
            player.MoveVertical(verticalOutput);
            player.MoveHorizontal(horizontalOutput);
        }

        // Only log when values change significantly
        if (Math.Abs(lastVertical - verticalOutput) > 0.1f || Math.Abs(lastHorizontal - horizontalOutput) > 0.1f || lastRunning != isRunning)
        {
            Log($"Movement - H: {horizontalOutput:F2} V: {verticalOutput:F2} Run: {isRunning}");
            lastVertical = verticalOutput;
            lastHorizontal = horizontalOutput;
            lastRunning = isRunning;
        }
    }

    private float lastVertical;
    private float lastHorizontal;
    private bool lastRunning;

    private static float Clamp(float value)
    {
        return Math.Max(-1.0f, Math.Min(value, 1.0f));
    }

    protected override Task<bool> OnModuleStart()
    {
        Log("OSCLeash module started");
        var player = GetPlayer();
        if (player != null)
        {
            player.StopRun();
            player.MoveVertical(0);
            player.MoveHorizontal(0);
        }
        return Task.FromResult(true);
    }
} 