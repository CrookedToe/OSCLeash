using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;

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

    // Cache previous values for delta updates
    private float lastVerticalOutput;
    private float lastHorizontalOutput;
    private float lastTurningOutput;
    private bool lastRunState;
    
    // Input smoothing
    private readonly Queue<float> verticalSmoothingBuffer = new(4);
    private readonly Queue<float> horizontalSmoothingBuffer = new(4);
    private const int SmoothingBufferSize = 4;

    private CancellationTokenSource? movementCts;

    private enum LeashDirection
    {
        North,
        South,
        East,
        West
    }

    private enum OSCLeashParameter
    {
        ZPositive,
        ZNegative,
        XPositive,
        XNegative,
        YPositive,
        YNegative,
        IsGrabbed,
        Stretch,
        // Output parameters
        Vertical,
        Horizontal,
        Run
    }

    private enum OSCLeashSetting
    {
        LeashDirection,
        RunDeadzone,
        WalkDeadzone,
        StrengthMultiplier,
        UpDownCompensation,
        UpDownDeadzone,
        TurningEnabled,
        TurningMultiplier,
        TurningDeadzone,
        TurningGoal
    }

    protected override void OnPreLoad()
    {
        // Create settings
        CreateDropdown(OSCLeashSetting.LeashDirection, "Leash Direction", "Direction the leash faces", LeashDirection.North);
        
        CreateSlider(OSCLeashSetting.RunDeadzone, "Run Deadzone", "Stretch threshold for running", 0.70f, 0.0f, 1.0f);
        CreateSlider(OSCLeashSetting.WalkDeadzone, "Walk Deadzone", "Stretch threshold for walking", 0.15f, 0.0f, 1.0f);
        CreateSlider(OSCLeashSetting.StrengthMultiplier, "Strength Multiplier", "Movement strength multiplier", 1.2f, 0.1f, 5.0f);
        CreateSlider(OSCLeashSetting.UpDownCompensation, "Up/Down Compensation", "Compensation for vertical movement", 1.0f, 0.0f, 2.0f);
        CreateSlider(OSCLeashSetting.UpDownDeadzone, "Up/Down Deadzone", "Vertical angle deadzone", 0.5f, 0.0f, 1.0f);
        
        // Turning settings
        CreateToggle(OSCLeashSetting.TurningEnabled, "Enable Turning", "Enable turning control with the leash", false);
        CreateSlider(OSCLeashSetting.TurningMultiplier, "Turning Multiplier", "Turning speed multiplier", 0.80f, 0.1f, 2.0f);
        CreateSlider(OSCLeashSetting.TurningDeadzone, "Turning Deadzone", "Minimum stretch required for turning", 0.15f, 0.0f, 1.0f);
        CreateSlider(OSCLeashSetting.TurningGoal, "Turning Goal", "Maximum turning angle in degrees", 90f, 0.0f, 180.0f);

        // Register physbone state parameters
        RegisterParameter<bool>(OSCLeashParameter.IsGrabbed, "Leash_IsGrabbed", ParameterMode.Read, "Leash Grabbed", "Physbone grab state");
        RegisterParameter<float>(OSCLeashParameter.Stretch, "Leash_Stretch", ParameterMode.Read, "Leash Stretch", "Physbone stretch value");
        
        // Direction parameters
        RegisterParameter<float>(OSCLeashParameter.ZPositive, "Leash_ZPositive", ParameterMode.Read, "Forward Direction", "Forward movement value", false);
        RegisterParameter<float>(OSCLeashParameter.ZNegative, "Leash_ZNegative", ParameterMode.Read, "Backward Direction", "Backward movement value", false);
        RegisterParameter<float>(OSCLeashParameter.XPositive, "Leash_XPositive", ParameterMode.Read, "Right Direction", "Right movement value", false);
        RegisterParameter<float>(OSCLeashParameter.XNegative, "Leash_XNegative", ParameterMode.Read, "Left Direction", "Left movement value", false);
        RegisterParameter<float>(OSCLeashParameter.YPositive, "Leash_YPositive", ParameterMode.Read, "Up Direction", "Upward movement value", false);
        RegisterParameter<float>(OSCLeashParameter.YNegative, "Leash_YNegative", ParameterMode.Read, "Down Direction", "Downward movement value", false);
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case OSCLeashParameter.IsGrabbed:
                isGrabbed = parameter.GetValue<bool>();
                break;
            case OSCLeashParameter.Stretch:
                stretch = parameter.GetValue<float>();
                break;
            case OSCLeashParameter.ZPositive:
                zPositive = parameter.GetValue<float>();
                break;
            case OSCLeashParameter.ZNegative:
                zNegative = parameter.GetValue<float>();
                break;
            case OSCLeashParameter.XPositive:
                xPositive = parameter.GetValue<float>();
                break;
            case OSCLeashParameter.XNegative:
                xNegative = parameter.GetValue<float>();
                break;
            case OSCLeashParameter.YPositive:
                yPositive = parameter.GetValue<float>();
                break;
            case OSCLeashParameter.YNegative:
                yNegative = parameter.GetValue<float>();
                break;
        }
    }

    private float SmoothValue(Queue<float> buffer, float newValue)
    {
        buffer.Enqueue(newValue);
        if (buffer.Count > SmoothingBufferSize)
            buffer.Dequeue();
        
        return buffer.Count > 0 ? buffer.Average() : newValue;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 33)] // Reduced to ~30Hz
    private void UpdateMovement()
    {
        try
        {
            var player = GetPlayer();
            if (player == null)
            {
                return;
            }

            // Only create new CTS if needed
            if (movementCts == null || movementCts.IsCancellationRequested)
            {
                movementCts?.Dispose();
                movementCts = new CancellationTokenSource();
            }

            if (!isGrabbed)
            {
                if (lastRunState || lastVerticalOutput != 0 || lastHorizontalOutput != 0 || lastTurningOutput != 0)
                {
                    ResetMovement(player);
                }
                return;
            }

            // Movement calculations with smoothing
            var strengthMultiplier = GetSettingValue<float>(OSCLeashSetting.StrengthMultiplier);
            var outputMultiplier = stretch * strengthMultiplier;
            
            var rawVerticalOutput = Clamp((zPositive - zNegative) * outputMultiplier);
            var rawHorizontalOutput = Clamp((xPositive - xNegative) * outputMultiplier);

            var verticalOutput = SmoothValue(verticalSmoothingBuffer, rawVerticalOutput);
            var horizontalOutput = SmoothValue(horizontalSmoothingBuffer, rawHorizontalOutput);

            // Up/Down compensation
            var yCombined = yPositive + yNegative;
            var upDownDeadzone = GetSettingValue<float>(OSCLeashSetting.UpDownDeadzone);

            if (yCombined >= upDownDeadzone)
            {
                if (lastRunState || lastVerticalOutput != 0 || lastHorizontalOutput != 0 || lastTurningOutput != 0)
                {
                    ResetMovement(player);
                }
                return;
            }

            // Up/Down Compensation
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

            // Turning calculation
            var turningOutput = CalculateTurningOutput();

            // Apply movement based on stretch with delta updates
            var runDeadzone = GetSettingValue<float>(OSCLeashSetting.RunDeadzone);
            var newRunState = stretch > runDeadzone;

            // Only update states that have changed
            if (newRunState != lastRunState)
            {
                if (newRunState)
                    player.Run();
                else
                    player.StopRun();
                lastRunState = newRunState;
            }

            // Apply movement only when values have changed significantly
            const float updateThreshold = 0.01f;
            
            if (Math.Abs(verticalOutput - lastVerticalOutput) > updateThreshold)
            {
                player.MoveVertical(verticalOutput);
                lastVerticalOutput = verticalOutput;
            }
            
            if (Math.Abs(horizontalOutput - lastHorizontalOutput) > updateThreshold)
            {
                player.MoveHorizontal(horizontalOutput);
                lastHorizontalOutput = horizontalOutput;
            }
            
            if (Math.Abs(turningOutput - lastTurningOutput) > updateThreshold)
            {
                player.LookHorizontal(turningOutput);
                lastTurningOutput = turningOutput;
            }
        }
        catch (Exception ex)
        {
            Log($"Error in UpdateMovement: {ex.Message}");
            try
            {
                var player = GetPlayer();
                if (player != null)
                {
                    ResetMovement(player);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void ResetMovement(Player player)
    {
        player.StopRun();
        player.MoveVertical(0);
        player.MoveHorizontal(0);
        player.LookHorizontal(0);
        
        lastRunState = false;
        lastVerticalOutput = 0;
        lastHorizontalOutput = 0;
        lastTurningOutput = 0;
        
        verticalSmoothingBuffer.Clear();
        horizontalSmoothingBuffer.Clear();
    }

    private float CalculateTurningOutput()
    {
        var turningEnabled = GetSettingValue<bool>(OSCLeashSetting.TurningEnabled);
        if (!turningEnabled || stretch <= GetSettingValue<float>(OSCLeashSetting.TurningDeadzone))
            return 0f;

        var turningMultiplier = GetSettingValue<float>(OSCLeashSetting.TurningMultiplier);
        var turningGoal = Math.Max(0f, GetSettingValue<float>(OSCLeashSetting.TurningGoal)) / 180f;
        var direction = GetSettingValue<LeashDirection>(OSCLeashSetting.LeashDirection);
        var horizontalOutput = Clamp((xPositive - xNegative) * stretch * GetSettingValue<float>(OSCLeashSetting.StrengthMultiplier));
        var verticalOutput = Clamp((zPositive - zNegative) * stretch * GetSettingValue<float>(OSCLeashSetting.StrengthMultiplier));

        float turningOutput = 0f;
        switch (direction)
        {
            case LeashDirection.North:
                if (zPositive < turningGoal)
                {
                    turningOutput = horizontalOutput * turningMultiplier;
                    if (xPositive > xNegative)
                        turningOutput += zNegative;
                    else
                        turningOutput -= zNegative;
                }
                break;
            case LeashDirection.South:
                if (zNegative < turningGoal)
                {
                    turningOutput = -horizontalOutput * turningMultiplier;
                    if (xPositive > xNegative)
                        turningOutput -= zPositive;
                    else
                        turningOutput += zPositive;
                }
                break;
            case LeashDirection.East:
                if (xPositive < turningGoal)
                {
                    turningOutput = verticalOutput * turningMultiplier;
                    if (zPositive > zNegative)
                        turningOutput += xNegative;
                    else
                        turningOutput -= xNegative;
                }
                break;
            case LeashDirection.West:
                if (xNegative < turningGoal)
                {
                    turningOutput = -verticalOutput * turningMultiplier;
                    if (zPositive > zNegative)
                        turningOutput -= xPositive;
                    else
                        turningOutput += xPositive;
                }
                break;
        }
        return Clamp(turningOutput);
    }

    private static float Clamp(float value)
    {
        return Math.Max(-1.0f, Math.Min(value, 1.0f));
    }

    protected override Task<bool> OnModuleStart()
    {
        Log("OSCLeash module started");
        movementCts = new CancellationTokenSource();
        
        var player = GetPlayer();
        if (player != null)
        {
            player.StopRun();
            player.MoveVertical(0);
            player.MoveHorizontal(0);
            player.LookHorizontal(0);
        }
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        movementCts?.Cancel();
        movementCts?.Dispose();
        movementCts = null;

        var player = GetPlayer();
        if (player != null)
        {
            player.StopRun();
            player.MoveVertical(0);
            player.MoveHorizontal(0);
            player.LookHorizontal(0);
        }

        return Task.CompletedTask;
    }
} 