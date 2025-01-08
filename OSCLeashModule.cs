using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using System.Numerics;

namespace VRCOSC.Modules.OSCLeash;

[ModuleTitle("OSC Leash")]
[ModuleDescription("Allows for controlling avatar movement with parameters")]
[ModuleType(ModuleType.Generic)]
public class OSCLeashModule : Module
{
    private const float MOVEMENT_EPSILON = 0.0001f;
    
    private Vector3 _positiveForces;
    private Vector3 _negativeForces;
    private float _currentTurnAngle;
    private float _lastUpdateTime;
    private bool _isGrabbed;
    private float _stretch;
    private string _currentBaseName = string.Empty;
    private string _currentDirection = "North";

    // Helper function for smooth interpolation
    private static float Lerp(float start, float end, float amount)
    {
        return start + (end - start) * amount;
    }

    protected override void OnPreLoad()
    {
        CreateCustomSetting(LeashSetting.Settings, new OSCLeashModuleSettings());
        var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
        
        // Validate base name
        if (string.IsNullOrEmpty(settings.LeashVariable.Value))
        {
            Log("Warning: Empty leash variable name, using 'Leash' as default");
            settings.LeashVariable.Value = "Leash";
        }
        
        // Validate movement settings
        if (settings.WalkDeadzone.Value < 0 || settings.WalkDeadzone.Value > 1)
        {
            Log($"Warning: Walk deadzone {settings.WalkDeadzone.Value} outside valid range [0,1], clamping");
            settings.WalkDeadzone.Value = Math.Clamp(settings.WalkDeadzone.Value, 0, 1);
        }
        
        if (settings.RunDeadzone.Value < settings.WalkDeadzone.Value || settings.RunDeadzone.Value > 1)
        {
            Log($"Warning: Run deadzone {settings.RunDeadzone.Value} invalid, clamping");
            settings.RunDeadzone.Value = Math.Clamp(settings.RunDeadzone.Value, settings.WalkDeadzone.Value, 1);
        }
        
        if (settings.MaxVelocity.Value <= 0)
        {
            Log($"Warning: Invalid max velocity {settings.MaxVelocity.Value}, using 1.0");
            settings.MaxVelocity.Value = 1.0f;
        }

        // Validate turning settings
        if (settings.TurningEnabled.Value)
        {
            if (settings.TurningMultiplier.Value < 0)
            {
                Log($"Warning: Negative turning multiplier {settings.TurningMultiplier.Value}, using absolute value");
                settings.TurningMultiplier.Value = Math.Abs(settings.TurningMultiplier.Value);
            }
            
            if (settings.TurningDeadzone.Value < 0 || settings.TurningDeadzone.Value > 1)
            {
                Log($"Warning: Turning deadzone {settings.TurningDeadzone.Value} outside valid range [0,1], clamping");
                settings.TurningDeadzone.Value = Math.Clamp(settings.TurningDeadzone.Value, 0, 1);
            }

            if (settings.SmoothTurningSpeed.Value < 0)
            {
                Log($"Warning: Negative smooth turning speed {settings.SmoothTurningSpeed.Value}, using absolute value");
                settings.SmoothTurningSpeed.Value = Math.Abs(settings.SmoothTurningSpeed.Value);
            }
        }

        var fullBaseName = settings.LeashVariable.Value;

        // Check for direction suffix in the base name
        var direction = settings.LeashDirection.Value; // Use current direction as default
        
        // Common direction suffixes
        if (fullBaseName.EndsWith("_North", StringComparison.OrdinalIgnoreCase))
        {
            direction = "North";
        }
        else if (fullBaseName.EndsWith("_South", StringComparison.OrdinalIgnoreCase))
        {
            direction = "South";
        }
        else if (fullBaseName.EndsWith("_East", StringComparison.OrdinalIgnoreCase))
        {
            direction = "East";
        }
        else if (fullBaseName.EndsWith("_West", StringComparison.OrdinalIgnoreCase))
        {
            direction = "West";
        }

        // Update direction setting if we found a direction suffix
        if (direction != settings.LeashDirection.Value)
        {
            settings.LeashDirection.Value = direction;
            Log($"Detected direction from parameter name: {direction}");
        }

        // Register core parameters using the full base name
        RegisterParameter<bool>(LeashParameter.IsGrabbed, $"{fullBaseName}_IsGrabbed", ParameterMode.Read, "Leash Grabbed", "Whether the leash is currently grabbed");
        RegisterParameter<float>(LeashParameter.Stretch, $"{fullBaseName}_Stretch", ParameterMode.Read, "Leash Stretch", "The stretch value of the leash physbone");
        
        // Register directional parameters
        RegisterParameter<float>(LeashParameter.ZPositive, $"{fullBaseName}_ZPositive", ParameterMode.Read, "Forward Pull", "Forward pulling force");
        RegisterParameter<float>(LeashParameter.XPositive, $"{fullBaseName}_XPositive", ParameterMode.Read, "Right Pull", "Right pulling force");
        RegisterParameter<float>(LeashParameter.YPositive, $"{fullBaseName}_YPositive", ParameterMode.Read, "Up Pull", "Upward pulling force");
        RegisterParameter<float>(LeashParameter.ZNegative, $"{fullBaseName}_Z-", ParameterMode.Read, "Backward Pull", "Backward pulling force");
        RegisterParameter<float>(LeashParameter.XNegative, $"{fullBaseName}_X-", ParameterMode.Read, "Left Pull", "Left pulling force");
        RegisterParameter<float>(LeashParameter.YNegative, $"{fullBaseName}_Y-", ParameterMode.Read, "Down Pull", "Downward pulling force");

        // Register output parameters
        RegisterParameter<float>(LeashParameter.Vertical, "/input/Vertical", ParameterMode.Write, "Vertical Movement", "Controls forward/backward movement");
        RegisterParameter<float>(LeashParameter.Horizontal, "/input/Horizontal", ParameterMode.Write, "Horizontal Movement", "Controls left/right movement");
        RegisterParameter<float>(LeashParameter.LookHorizontal, "/input/LookHorizontal", ParameterMode.Write, "Look Horizontal", "Controls turning");
        RegisterParameter<bool>(LeashParameter.Run, "/input/Run", ParameterMode.Write, "Run", "Controls running state");

        // Subscribe to variable name changes
        settings.LeashVariable.Subscribe(OnVariableNameChanged);
        settings.LeashDirection.Subscribe(OnDirectionChanged);
        
        _currentBaseName = fullBaseName;
        _currentDirection = direction;
    }

    private void OnVariableNameChanged(string newName)
    {
        _currentBaseName = newName;
        OnPreLoad();
    }

    private void OnDirectionChanged(string newDirection)
    {
        _currentDirection = newDirection;
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        if (parameter == null) return;
        var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
        
        switch (parameter.Lookup)
        {
            case LeashParameter.IsGrabbed:
                _isGrabbed = parameter.GetValue<bool>();
                if (!_isGrabbed)
                {
                    ResetMovement();
                }
                break;

            case LeashParameter.Stretch:
                _stretch = Math.Clamp(parameter.GetValue<float>(), 0f, 1f);
                break;

            case LeashParameter.ZPositive:
                _positiveForces.Z = Math.Max(0f, parameter.GetValue<float>());
                break;

            case LeashParameter.ZNegative:
                _negativeForces.Z = Math.Max(0f, parameter.GetValue<float>());
                break;

            case LeashParameter.XPositive:
                _positiveForces.X = Math.Max(0f, parameter.GetValue<float>());
                break;

            case LeashParameter.XNegative:
                _negativeForces.X = Math.Max(0f, parameter.GetValue<float>());
                break;

            case LeashParameter.YPositive:
                _positiveForces.Y = Math.Max(0f, parameter.GetValue<float>());
                break;

            case LeashParameter.YNegative:
                _negativeForces.Y = Math.Max(0f, parameter.GetValue<float>());
                break;
        }

        if (_isGrabbed)
        {
            UpdateMovement(settings);
        }
    }

    private void UpdateMovement(OSCLeashModuleSettings settings)
    {
        var currentTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
        var deltaTime = Math.Max(currentTime - _lastUpdateTime, 0.016f);
        _lastUpdateTime = currentTime;

        // Calculate base movement values
        var outputMultiplier = _stretch * settings.StrengthMultiplier.Value;
        var verticalOutput = (_positiveForces.Z - _negativeForces.Z) * outputMultiplier;
        var horizontalOutput = (_positiveForces.X - _negativeForces.X) * outputMultiplier;

        // Calculate movement strength
        var baseStrength = MathF.Sqrt(verticalOutput * verticalOutput + horizontalOutput * horizontalOutput);

        // Apply safety limits if enabled
        if (settings.EnableSafetyLimits.Value)
        {
            verticalOutput = Math.Clamp(verticalOutput, -settings.MaxVelocity.Value, settings.MaxVelocity.Value);
            horizontalOutput = Math.Clamp(horizontalOutput, -settings.MaxVelocity.Value, settings.MaxVelocity.Value);
        }
        
        // Check walk and run thresholds
        if (baseStrength < settings.WalkDeadzone.Value)
        {
            verticalOutput = 0;
            horizontalOutput = 0;
            baseStrength = 0;
        }
        var isRunning = baseStrength >= settings.RunDeadzone.Value;

        // Calculate turning if enabled
        var turningSpeed = 0f;
        if (settings.TurningEnabled.Value && baseStrength >= settings.TurningDeadzone.Value)
        {
            turningSpeed = settings.TurningMultiplier.Value;
            
            switch (_currentDirection.ToLower())
            {
                case "north":
                    turningSpeed *= horizontalOutput;
                    if (_positiveForces.X > _negativeForces.X)
                    {
                        turningSpeed += _negativeForces.Z;
                    }
                    else
                    {
                        turningSpeed -= _negativeForces.Z;
                    }
                    break;

                case "south":
                    turningSpeed *= -horizontalOutput;
                    if (_positiveForces.X > _negativeForces.X)
                    {
                        turningSpeed -= _positiveForces.Z;
                    }
                    else
                    {
                        turningSpeed += _positiveForces.Z;
                    }
                    break;

                case "east":
                    turningSpeed *= verticalOutput;
                    if (_positiveForces.Z > _negativeForces.Z)
                    {
                        turningSpeed += _negativeForces.X;
                    }
                    else
                    {
                        turningSpeed -= _negativeForces.X;
                    }
                    break;

                case "west":
                    turningSpeed *= -verticalOutput;
                    if (_positiveForces.Z > _negativeForces.Z)
                    {
                        turningSpeed -= _positiveForces.X;
                    }
                    else
                    {
                        turningSpeed += _positiveForces.X;
                    }
                    break;
            }

            turningSpeed = Math.Clamp(turningSpeed, -1f, 1f);
            _currentTurnAngle = Lerp(_currentTurnAngle, turningSpeed, settings.SmoothTurningSpeed.Value * deltaTime);
        }
        else
        {
            _currentTurnAngle = 0;
        }

        // Apply deadzone to movement values
        if (Math.Abs(horizontalOutput) < MOVEMENT_EPSILON) horizontalOutput = 0;
        if (Math.Abs(verticalOutput) < MOVEMENT_EPSILON) verticalOutput = 0;

        SendMovementValues(horizontalOutput, verticalOutput, _currentTurnAngle, isRunning);
    }

    private void SendMovementValues(float horizontal, float vertical, float turn, bool isRunning)
    {
        try
        {
            var player = GetPlayer();
            if (player != null)
            {
                player.MoveVertical(vertical);
                player.MoveHorizontal(horizontal);
                player.LookHorizontal(turn);
                if (isRunning) player.Run();
                else player.StopRun();
            }
            else
            {
                SendParameter(LeashParameter.Vertical, vertical);
                SendParameter(LeashParameter.Horizontal, horizontal);
                SendParameter(LeashParameter.LookHorizontal, turn);
                SendParameter(LeashParameter.Run, isRunning);
            }
        }
        catch (Exception e)
        {
            Log($"Error sending movement values: {e.Message}");
            SendParameter(LeashParameter.Vertical, vertical);
            SendParameter(LeashParameter.Horizontal, horizontal);
            SendParameter(LeashParameter.LookHorizontal, turn);
            SendParameter(LeashParameter.Run, isRunning);
        }
    }

    private void ResetMovement()
    {
        _positiveForces = Vector3.Zero;
        _negativeForces = Vector3.Zero;
        _currentTurnAngle = 0;
        SendMovementValues(0, 0, 0, false);
    }

    protected override void OnAvatarChange(AvatarConfig? avatarConfig)
    {
        ResetMovement();
    }

    private enum LeashSetting
    {
        Settings
    }

    private enum LeashParameter
    {
        IsGrabbed,
        Stretch,
        ZPositive,
        ZNegative,
        XPositive,
        XNegative,
        YPositive,
        YNegative,
        Vertical,
        Horizontal,
        LookHorizontal,
        Run
    }
} 