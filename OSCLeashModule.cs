using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using System.Numerics;
using System.Threading.Tasks;

namespace VRCOSC.Modules.OSCLeash;

[ModuleTitle("OSC Leash")]
[ModuleDescription("Allows for controlling avatar movement with parameters")]
[ModuleType(ModuleType.Generic)]
[ModulePrefab("OSCLeash", "https://github.com/CrookedToe/OSCLeash/tree/main/Unity")]
[ModuleInfo("https://github.com/CrookedToe/OSCLeash")]
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
    
    // Movement state tracking
    private float _currentVertical;
    private float _currentHorizontal;
    private bool _currentRunState;
    private float _transitionTimer;

    // Helper function for smooth interpolation
    private static float Lerp(float start, float end, float amount)
    {
        return start + (end - start) * amount;
    }

    protected override void OnPreLoad()
    {
        try
        {
            CreateCustomSetting(LeashSetting.Settings, new OSCLeashModuleSettings());
            var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
            
            LogDebug("Initializing OSCLeash module...");
            
            // Validate base name
            if (string.IsNullOrEmpty(settings.LeashVariable.Value))
            {
                Log("Warning: Empty leash variable name, using 'Leash' as default");
                settings.LeashVariable.Value = "Leash";
            }
            
            // Register parameters
            var baseName = settings.LeashVariable.Value;
            RegisterParameter<bool>(LeashParameter.IsGrabbed, $"{baseName}_IsGrabbed", ParameterMode.Read, "Is Grabbed", "Whether the leash is currently grabbed");
            RegisterParameter<float>(LeashParameter.Stretch, $"{baseName}_Stretch", ParameterMode.Read, "Stretch", "How much the leash is stretched");
            RegisterParameter<float>(LeashParameter.ZPositive, $"{baseName}_ZPositive", ParameterMode.Read, "Forward Force", "Forward movement force");
            RegisterParameter<float>(LeashParameter.ZNegative, $"{baseName}_ZNegative", ParameterMode.Read, "Backward Force", "Backward movement force");
            RegisterParameter<float>(LeashParameter.XPositive, $"{baseName}_XPositive", ParameterMode.Read, "Right Force", "Right movement force");
            RegisterParameter<float>(LeashParameter.XNegative, $"{baseName}_XNegative", ParameterMode.Read, "Left Force", "Left movement force");
            RegisterParameter<float>(LeashParameter.YPositive, $"{baseName}_YPositive", ParameterMode.Read, "Up Force", "Upward movement force");
            RegisterParameter<float>(LeashParameter.YNegative, $"{baseName}_YNegative", ParameterMode.Read, "Down Force", "Downward movement force");
            
            LogDebug("OSCLeash module initialized successfully");
        }
        catch (Exception e)
        {
            Log($"Failed to initialize OSCLeash module: {e.Message}");
            LogDebug($"Stack trace: {e.StackTrace}");
        }
    }

    private void RegisterParameters(string baseName)
    {
        LogDebug($"Registering parameters with base name: {baseName}");
        
        // Register core parameters
        RegisterParameter<bool>(LeashParameter.IsGrabbed, $"{baseName}_IsGrabbed", ParameterMode.Read, 
            "Leash Grabbed", "Whether the leash is currently grabbed");
        RegisterParameter<float>(LeashParameter.Stretch, $"{baseName}_Stretch", ParameterMode.Read, 
            "Leash Stretch", "The stretch value of the leash physbone");
        
        // Register directional parameters
        RegisterParameter<float>(LeashParameter.ZPositive, $"{baseName}_ZPositive", ParameterMode.Read, 
            "Forward Pull", "Forward pulling force");
        RegisterParameter<float>(LeashParameter.XPositive, $"{baseName}_XPositive", ParameterMode.Read, 
            "Right Pull", "Right pulling force");
        RegisterParameter<float>(LeashParameter.YPositive, $"{baseName}_YPositive", ParameterMode.Read, 
            "Up Pull", "Upward pulling force");
        RegisterParameter<float>(LeashParameter.ZNegative, $"{baseName}_Z-", ParameterMode.Read, 
            "Backward Pull", "Backward pulling force");
        RegisterParameter<float>(LeashParameter.XNegative, $"{baseName}_X-", ParameterMode.Read, 
            "Left Pull", "Left pulling force");
        RegisterParameter<float>(LeashParameter.YNegative, $"{baseName}_Y-", ParameterMode.Read, 
            "Down Pull", "Downward pulling force");
        
        LogDebug("Parameter registration complete");
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        if (parameter == null) return;
        var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
        
        switch (parameter.Lookup)
        {
            case LeashParameter.IsGrabbed:
                var newGrabbed = parameter.GetValue<bool>();
                if (_isGrabbed != newGrabbed)
                {
                    LogDebug($"Leash grab state changed: {newGrabbed}");
                    _isGrabbed = newGrabbed;
                    if (!_isGrabbed)
                    {
                        LogDebug("Leash released, resetting movement");
                        ResetMovement();
                    }
                }
                break;

            case LeashParameter.Stretch:
                _stretch = Math.Clamp(parameter.GetValue<float>(), 0f, 1f);
                LogDebug($"Leash stretch updated: {_stretch:F3}");
                break;

            case LeashParameter.ZPositive:
                _positiveForces.Z = Math.Max(0f, parameter.GetValue<float>());
                LogDebug($"Forward force updated: {_positiveForces.Z:F3}");
                break;

            case LeashParameter.ZNegative:
                _negativeForces.Z = Math.Max(0f, parameter.GetValue<float>());
                LogDebug($"Backward force updated: {_negativeForces.Z:F3}");
                break;

            case LeashParameter.XPositive:
                _positiveForces.X = Math.Max(0f, parameter.GetValue<float>());
                LogDebug($"Right force updated: {_positiveForces.X:F3}");
                break;

            case LeashParameter.XNegative:
                _negativeForces.X = Math.Max(0f, parameter.GetValue<float>());
                LogDebug($"Left force updated: {_negativeForces.X:F3}");
                break;

            case LeashParameter.YPositive:
                _positiveForces.Y = Math.Max(0f, parameter.GetValue<float>());
                LogDebug($"Up force updated: {_positiveForces.Y:F3}");
                break;

            case LeashParameter.YNegative:
                _negativeForces.Y = Math.Max(0f, parameter.GetValue<float>());
                LogDebug($"Down force updated: {_negativeForces.Y:F3}");
                break;
        }

        if (_isGrabbed)
        {
            UpdateMovement(settings);
        }
    }

    private void StartNewTransition()
    {
        _transitionTimer = 0f;
    }

    protected override void OnAvatarChange(AvatarConfig? avatarConfig)
    {
        // Reset all movement state when avatar changes
        ResetMovement();
        
        // Clear any stored state
        _positiveForces = Vector3.Zero;
        _negativeForces = Vector3.Zero;
        _currentTurnAngle = 0f;
        _currentVertical = 0f;
        _currentHorizontal = 0f;
        _currentRunState = false;
        _transitionTimer = 0f;
        _isGrabbed = false;
        _stretch = 0f;
    }

    private void UpdateMovement(OSCLeashModuleSettings settings)
    {
        var currentTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
        var deltaTime = Math.Max(currentTime - _lastUpdateTime, 0.016f);
        _lastUpdateTime = currentTime;

        // Calculate base movement values
        var outputMultiplier = _stretch * settings.StrengthMultiplier.Value;
        var targetVertical = (_positiveForces.Z - _negativeForces.Z) * outputMultiplier;
        var targetHorizontal = (_positiveForces.X - _negativeForces.X) * outputMultiplier;

        // Calculate movement strength
        var baseStrength = MathF.Sqrt(targetVertical * targetVertical + targetHorizontal * targetHorizontal);

        // Apply safety limits if enabled
        if (settings.EnableSafetyLimits.Value)
        {
            var originalVertical = targetVertical;
            var originalHorizontal = targetHorizontal;
            targetVertical = Math.Clamp(targetVertical, -settings.MaxVelocity.Value, settings.MaxVelocity.Value);
            targetHorizontal = Math.Clamp(targetHorizontal, -settings.MaxVelocity.Value, settings.MaxVelocity.Value);
        }
        
        // Check walk and run thresholds
        var targetRunState = false;
        var movementStateChanged = false;

        if (baseStrength < settings.WalkDeadzone.Value)
        {
            if (targetVertical != 0 || targetHorizontal != 0)
            {
                movementStateChanged = true;
            }
            targetVertical = 0;
            targetHorizontal = 0;
            baseStrength = 0;
        }
        else
        {
            // Check if we should be running
            targetRunState = baseStrength >= settings.RunDeadzone.Value;
            
            // Add hysteresis for run state to prevent flickering
            if (targetRunState != _currentRunState)
            {
                // If we're transitioning to running, require a bit more force
                if (targetRunState && baseStrength < settings.RunDeadzone.Value * 1.1f)
                {
                    targetRunState = false;
                }
                // If we're transitioning to walking, require a bit less force
                else if (!targetRunState && baseStrength > settings.RunDeadzone.Value * 0.9f)
                {
                    targetRunState = true;
                }
                
                if (targetRunState != _currentRunState)
                {
                    movementStateChanged = true;
                }
            }
        }

        // Reset transition timer on movement state changes
        if (movementStateChanged)
        {
            StartNewTransition();
        }

        // Calculate turning if enabled
        var targetTurnAngle = 0f;
        if (settings.TurningEnabled.Value && baseStrength >= settings.TurningDeadzone.Value)
        {
            var turningSpeed = settings.TurningMultiplier.Value;
            
            switch (_currentDirection.ToLower())
            {
                case "north":
                    turningSpeed *= targetHorizontal;
                    break;

                case "south":
                    turningSpeed *= -targetHorizontal;
                    break;

                case "east":
                    turningSpeed *= -targetVertical;
                    break;

                case "west":
                    turningSpeed *= targetVertical;
                    break;
            }

            targetTurnAngle = turningSpeed * settings.SmoothTurningSpeed.Value;
        }

        // Update transition timer
        _transitionTimer = Math.Min(_transitionTimer + deltaTime, settings.StateTransitionTime.Value);
        var transitionProgress = settings.StateTransitionTime.Value > 0 
            ? _transitionTimer / settings.StateTransitionTime.Value 
            : 1f;

        // Smoothly interpolate movement values
        _currentVertical = Lerp(_currentVertical, targetVertical, transitionProgress);
        _currentHorizontal = Lerp(_currentHorizontal, targetHorizontal, transitionProgress);
        _currentTurnAngle = Lerp(_currentTurnAngle, targetTurnAngle, transitionProgress);
        
        // Update run state with hysteresis to prevent flickering
        if (_currentRunState != targetRunState && transitionProgress >= 1f)
        {
            _currentRunState = targetRunState;
        }

        // Apply deadzone to movement values
        if (Math.Abs(_currentHorizontal) < MOVEMENT_EPSILON) _currentHorizontal = 0;
        if (Math.Abs(_currentVertical) < MOVEMENT_EPSILON) _currentVertical = 0;
        if (Math.Abs(_currentTurnAngle) < MOVEMENT_EPSILON) _currentTurnAngle = 0;

        // Send movement values using player methods
        SendMovementValues(_currentHorizontal, _currentVertical, _currentTurnAngle, _currentRunState);
    }

    private void SendMovementValues(float horizontal, float vertical, float turn, bool isRunning)
    {
        try
        {
            var player = GetPlayer();
            if (player == null)
            {
                return;
            }

            // Send run state first to ensure it's applied before movement
            if (isRunning)
            {
                player.Run();
                // Send movement after a small delay to ensure run state is applied
                Task.Delay(16).ContinueWith(_ =>
                {
                    player.MoveVertical(vertical);
                    player.MoveHorizontal(horizontal);
                    player.LookHorizontal(turn);
                });
            }
            else
            {
                player.StopRun();
                player.MoveVertical(vertical);
                player.MoveHorizontal(horizontal);
                player.LookHorizontal(turn);
            }
        }
        catch (Exception e)
        {
            Log($"Error sending movement values: {e.Message}");
            
            // Try to recover by resetting movement
            try
            {
                ResetMovement();
            }
            catch (Exception resetError)
            {
                Log($"Failed to reset movement: {resetError.Message}");
            }
        }
    }

    private void ResetMovement()
    {
        _positiveForces = Vector3.Zero;
        _negativeForces = Vector3.Zero;
        _currentTurnAngle = 0f;
        _currentVertical = 0f;
        _currentHorizontal = 0f;
        _currentRunState = false;
        _transitionTimer = 0f;
        
        try
        {
            var player = GetPlayer();
            if (player == null)
            {
                LogDebug("Player object not available for reset");
                return;
            }

            // Reset movement using player methods
            player.MoveVertical(0f);
            player.MoveHorizontal(0f);
            player.LookHorizontal(0f);
            player.StopRun();
        }
        catch (Exception e)
        {
            Log($"Failed to send reset movement values: {e.Message}");
            LogDebug($"Stack trace: {e.StackTrace}");
        }
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
        YNegative
    }
} 