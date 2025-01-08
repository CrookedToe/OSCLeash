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
    private Vector3 _currentMovement;
    private float _currentTurnAngle;
    private float _lastUpdateTime;
    private bool _isGrabbed;
    private float _stretch;
    private string _currentBaseName;
    private string _currentDirection = "North";

    // Helper function for smooth interpolation
    private static float Lerp(float start, float end, float amount)
    {
        return start + (end - start) * amount;
    }

    private static Vector3 SmoothDamp(Vector3 current, Vector3 target, float smoothTime, float deltaTime)
    {
        smoothTime = Math.Max(0.0001f, smoothTime);
        float omega = 2f / smoothTime;
        float x = omega * deltaTime;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        
        return Vector3.Lerp(current, target, 1f - exp);
    }

    protected override void OnPreLoad()
    {
        CreateCustomSetting(LeashSetting.Settings, new OSCLeashModuleSettings());
        var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
        var baseName = settings.LeashVariable.Value;

        // Register core parameters
        RegisterParameter<bool>(LeashParameter.IsGrabbed, $"{baseName}_IsGrabbed", ParameterMode.Read, "Leash Grabbed", "Whether the leash is currently grabbed");
        RegisterParameter<float>(LeashParameter.Stretch, $"{baseName}_Stretch", ParameterMode.Read, "Leash Stretch", "The stretch value of the leash physbone");
        
        // Register directional parameters
        RegisterParameter<float>(LeashParameter.ZPositive, $"{baseName}_ZPositive", ParameterMode.Read, "Forward Pull", "Forward pulling force");
        RegisterParameter<float>(LeashParameter.XPositive, $"{baseName}_XPositive", ParameterMode.Read, "Right Pull", "Right pulling force");
        RegisterParameter<float>(LeashParameter.YPositive, $"{baseName}_YPositive", ParameterMode.Read, "Up Pull", "Upward pulling force");
        RegisterParameter<float>(LeashParameter.ZNegative, $"{baseName}_Z-", ParameterMode.Read, "Backward Pull", "Backward pulling force");
        RegisterParameter<float>(LeashParameter.XNegative, $"{baseName}_X-", ParameterMode.Read, "Left Pull", "Left pulling force");
        RegisterParameter<float>(LeashParameter.YNegative, $"{baseName}_Y-", ParameterMode.Read, "Down Pull", "Downward pulling force");

        // Register output parameters
        RegisterParameter<float>(LeashParameter.Vertical, "/input/Vertical", ParameterMode.Write, "Vertical Movement", "Controls forward/backward movement");
        RegisterParameter<float>(LeashParameter.Horizontal, "/input/Horizontal", ParameterMode.Write, "Horizontal Movement", "Controls left/right movement");
        RegisterParameter<float>(LeashParameter.LookHorizontal, "/input/LookHorizontal", ParameterMode.Write, "Look Horizontal", "Controls turning");
        RegisterParameter<bool>(LeashParameter.Run, "/input/Run", ParameterMode.Write, "Run", "Controls running state");

        // Subscribe to variable name changes
        settings.LeashVariable.Subscribe(OnVariableNameChanged);
        settings.LeashDirection.Subscribe(OnDirectionChanged);
        
        _currentBaseName = baseName;
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
                _stretch = parameter.GetValue<float>();
                break;

            case LeashParameter.ZPositive:
                _positiveForces.Z = parameter.GetValue<float>();
                break;

            case LeashParameter.ZNegative:
                _negativeForces.Z = parameter.GetValue<float>();
                break;

            case LeashParameter.XPositive:
                _positiveForces.X = parameter.GetValue<float>();
                break;

            case LeashParameter.XNegative:
                _negativeForces.X = parameter.GetValue<float>();
                break;

            case LeashParameter.YPositive:
                _positiveForces.Y = parameter.GetValue<float>();
                break;

            case LeashParameter.YNegative:
                _negativeForces.Y = parameter.GetValue<float>();
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
        var deltaTime = Math.Max(currentTime - _lastUpdateTime, 0.016f); // Cap at ~60fps
        _lastUpdateTime = currentTime;

        var forces = (_positiveForces - _negativeForces) * _stretch * settings.StrengthMultiplier.Value;
        
        if (settings.EnableSafetyLimits.Value)
        {
            forces = Vector3.Clamp(forces, new Vector3(-settings.MaxVelocity.Value), new Vector3(settings.MaxVelocity.Value));
        }

        // Smooth the movement
        _currentMovement = SmoothDamp(_currentMovement, forces, settings.StateTransitionTime.Value, deltaTime);
        
        var strength = _currentMovement.Length();
        var isRunning = strength >= settings.RunDeadzone.Value;
        
        // Calculate turn amount if turning is enabled
        var targetTurnAmount = 0f;
        if (settings.TurningEnabled.Value && strength >= settings.TurningDeadzone.Value)
        {
            var angle = (float)Math.Atan2(_currentMovement.X, _currentMovement.Z);
            targetTurnAmount = angle * settings.TurningMultiplier.Value;
            
            // Adjust turning based on direction
            switch (_currentDirection.ToLower())
            {
                case "south":
                    targetTurnAmount = -targetTurnAmount;
                    break;
                case "east":
                    targetTurnAmount = targetTurnAmount - MathF.PI / 2;
                    break;
                case "west":
                    targetTurnAmount = targetTurnAmount + MathF.PI / 2;
                    break;
            }
        }

        // Smooth the turning
        _currentTurnAngle = Lerp(_currentTurnAngle, targetTurnAmount, settings.SmoothTurningSpeed.Value * deltaTime);

        SendMovementValues(_currentMovement.X, _currentMovement.Z, _currentTurnAngle, isRunning);
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
        _currentMovement = Vector3.Zero;  // Reset smoothed movement
        _currentTurnAngle = 0;            // Reset smoothed turning
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