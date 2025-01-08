using System.Runtime.CompilerServices;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using VRCOSC.Modules.OSCLeash.Constants;
using VRCOSC.Modules.OSCLeash.Enums;
using VRCOSC.Modules.OSCLeash.Models;

namespace VRCOSC.Modules.OSCLeash;

/// <summary>
/// Module for controlling avatar movement through leash parameters.
/// Handles parameter processing, movement calculation, and state management.
/// </summary>
[ModuleTitle("OSC Leash")]
[ModuleDescription("Allows for controlling avatar movement with parameters")]
[ModuleType(ModuleType.Generic)]
[ModulePrefab("OSCLeash", "https://github.com/CrookedToe/OSCLeash/tree/main/Unity")]
public class OSCLeashModule : Module
{
    private readonly object _settingsLock = new();
    private volatile bool _isDisposed;

    private LeashSettings _settings;
    private LeashParameterHandler _parameterHandler;
    private LeashMovementCalculator _movementCalculator;
    private LeashStateManager _stateManager;

    public OSCLeashModule()
    {
        _parameterHandler = new LeashParameterHandler(Log);
        _movementCalculator = new LeashMovementCalculator();
        _stateManager = new LeashStateManager(Log, (param, value) => SendParameter((Enum)param, value));
    }

    protected override void OnPreLoad()
    {
        try
        {
            CreateCustomSetting(LeashSetting.Settings, new OSCLeashModuleSettings());
            var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
            
            if (string.IsNullOrEmpty(settings.LeashVariable.Value))
            {
                Log("Warning: Leash variable name is empty, using default");
                settings.LeashVariable.Value = "Leash";
            }
            
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
            settings.EnableDebugLogging.Subscribe(OnDebugLoggingChanged);
        }
        catch (Exception ex)
        {
            Log($"Error in OnPreLoad: {ex.Message}");
            throw; // Rethrow to prevent partial initialization
        }
    }

    protected override void OnPostLoad()
    {
        try
        {
            var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
            ValidateAndClampSettings(settings);
            _settings = LeashSettings.FromSettings(settings);
            SubscribeToSettingChanges(settings);
        }
        catch (Exception ex)
        {
            Log($"Error in OnPostLoad: {ex.Message}");
            throw;
        }
    }

    private void ValidateAndClampSettings(OSCLeashModuleSettings settings)
    {
        if (settings.WalkDeadzone.Value < 0 || settings.WalkDeadzone.Value > 1)
        {
            Log($"Warning: Walk deadzone value {settings.WalkDeadzone.Value} is outside valid range [0,1], clamping");
            settings.WalkDeadzone.Value = Math.Clamp(settings.WalkDeadzone.Value, 0, 1);
        }
        
        if (settings.RunDeadzone.Value < settings.WalkDeadzone.Value || settings.RunDeadzone.Value > 1)
        {
            Log($"Warning: Run deadzone value {settings.RunDeadzone.Value} is invalid, clamping");
            settings.RunDeadzone.Value = Math.Clamp(settings.RunDeadzone.Value, settings.WalkDeadzone.Value, 1);
        }
        
        // ... other validations ...
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        try
        {
            _parameterHandler.EnqueueParameter(parameter);
            ProcessParameters();
        }
        catch (Exception e)
        {
            Log($"Error processing parameter: {e.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessParameters()
    {
        if (_isDisposed) return;

        var processedCount = 0;
        var parameters = new List<RegisteredParameter?>(LeashConstants.BATCH_SIZE);

        try
        {
            while (!_isDisposed && processedCount < LeashConstants.BATCH_SIZE && 
                   _parameterHandler.TryDequeueParameter(out var parameter))
            {
                if (_parameterHandler.ValidateParameter(parameter))
                {
                    parameters.Add(parameter);
                    processedCount++;
                }
            }

            if (parameters.Count > 0 && !_isDisposed)
            {
                foreach (var parameter in parameters)
                {
                    if (parameter != null)
                    {
                        HandleParameter(parameter);
                    }
                }

                if (_stateManager.State.IsGrabbed && _stateManager.State.IsMoving)
                {
                    UpdateMovement();
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error processing parameter queue: {ex.Message}");
        }
    }

    private void HandleParameter(RegisteredParameter? parameter)
    {
        if (parameter == null) return;
        var paramName = parameter.Name;
        var direction = ParseLeashDirection(paramName);
        var baseName = GetBaseParameterName(paramName);

        if (direction != LeashDirection.None && 
            (_stateManager.CurrentLeashPoint.Direction == LeashDirection.None || 
             _stateManager.CurrentLeashPoint.BaseName != baseName))
        {
            _stateManager.CurrentLeashPoint = new LeashPoint
            {
                Direction = direction,
                BaseName = baseName,
                IsActive = true
            };
        }

        UpdateParameterValue(parameter);
    }

    private void UpdateMovement()
    {
        var currentTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
        var deltaTime = Math.Max(currentTime - _stateManager.State.LastUpdateTime, LeashConstants.MovementLimits.MinimumTimeStep);
        _stateManager.UpdateLastUpdateTime(currentTime);

        LeashSettings currentSettings;
        lock (_settingsLock)
        {
            currentSettings = _settings;
        }

        var forces = _movementCalculator.CalculateForces(_stateManager.State, currentSettings, deltaTime);
        var strength = forces.Length();
        _stateManager.UpdateCurrentStrength(strength);
        
        _stateManager.UpdateRunningState(strength, currentSettings.RunDeadzone);

        if (currentSettings.TurningEnabled)
        {
            var turnAmount = _movementCalculator.CalculateTurnAmount(forces, currentSettings, _stateManager.CurrentLeashPoint.Direction);
            turnAmount = _movementCalculator.ProcessTurning(_stateManager.State, turnAmount, currentSettings, deltaTime);
            
            if (currentSettings.EnableSafetyLimits)
            {
                turnAmount = Math.Clamp(turnAmount, -currentSettings.MaxTurnRate * deltaTime, currentSettings.MaxTurnRate * deltaTime);
            }
            
            _stateManager.UpdateTurningState(true);
            _stateManager.UpdateCurrentTurnAngle(turnAmount);
        }
        else
        {
            _stateManager.UpdateTurningState(false);
            _stateManager.UpdateCurrentTurnAngle(0);
        }
        
        var player = GetPlayer();
        _stateManager.SendMovementValues(forces, _stateManager.State.CurrentTurnAngle, currentSettings, player);
    }

    private void OnDebugLoggingChanged(bool enabled)
    {
        _parameterHandler = new LeashParameterHandler(Log, enabled);
        _stateManager = new LeashStateManager(Log, (param, value) => SendParameter((Enum)param, value), enabled);
    }

    protected override async Task OnModuleStop()
    {
        if (_isDisposed) return;

        try
        {
            _isDisposed = true;
            await Task.Run(() => {
                _parameterHandler.Clear();
                _stateManager.Reset();
                _stateManager.SendMovementValues(System.Numerics.Vector3.Zero, 0, _settings);
            });
        }
        catch (Exception ex)
        {
            Log($"Error during module stop: {ex.Message}");
        }

        await base.OnModuleStop();
    }

    protected override void OnAvatarChange(AvatarConfig? avatarConfig)
    {
        _stateManager.Reset();
        _stateManager.SendMovementValues(System.Numerics.Vector3.Zero, 0, _settings);
    }

    private void OnVariableNameChanged(string newName)
    {
        if (string.IsNullOrEmpty(newName))
        {
            Log("Warning: Empty leash variable name received");
            return;
        }
        OnPreLoad();
    }

    private void OnDirectionChanged(string newDirection)
    {
        if (!Enum.TryParse<LeashDirection>(newDirection, true, out var direction))
        {
            Log($"Warning: Invalid leash direction '{newDirection}', defaulting to North");
            direction = LeashDirection.North;
        }

        _stateManager.CurrentLeashPoint = new LeashPoint
            { 
                Direction = direction,
                BaseName = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings).LeashVariable.Value,
                IsActive = true
            };
    }

    private void SubscribeToSettingChanges(OSCLeashModuleSettings settings)
    {
        settings.WalkDeadzone.Subscribe(value => UpdateSetting(s => s with { WalkDeadzone = value }));
        settings.RunDeadzone.Subscribe(value => UpdateSetting(s => s with { RunDeadzone = value }));
        settings.StrengthMultiplier.Subscribe(value => UpdateSetting(s => s with { StrengthMultiplier = value }));
        settings.TurningEnabled.Subscribe(value => UpdateSetting(s => s with { TurningEnabled = value }));
        settings.TurningMultiplier.Subscribe(value => UpdateSetting(s => s with { TurningMultiplier = value }));
        settings.TurningDeadzone.Subscribe(value => UpdateSetting(s => s with { TurningDeadzone = value }));
        settings.TurningGoal.Subscribe(value => UpdateSetting(s => s with { TurningGoal = value }));
        settings.SmoothTurningSpeed.Subscribe(value => UpdateSetting(s => s with { SmoothTurningSpeed = value }));
        settings.TurningMomentum.Subscribe(value => UpdateSetting(s => s with { TurningMomentum = value }));
        settings.UpDownCompensation.Subscribe(value => UpdateSetting(s => s with { UpDownCompensation = value }));
        settings.UpDownDeadzone.Subscribe(value => UpdateSetting(s => s with { UpDownDeadzone = value }));
        settings.EnableSafetyLimits.Subscribe(value => UpdateSetting(s => s with { EnableSafetyLimits = value }));
        settings.MaxVelocity.Subscribe(value => UpdateSetting(s => s with { MaxVelocity = value }));
        settings.MaxAcceleration.Subscribe(value => UpdateSetting(s => s with { MaxAcceleration = value }));
        settings.MaxTurnRate.Subscribe(value => UpdateSetting(s => s with { MaxTurnRate = value }));
        settings.MovementCurveType.Subscribe(value => UpdateSetting(s => s with { MovementCurveType = value }));
        settings.CurveExponent.Subscribe(value => UpdateSetting(s => s with { CurveExponent = value }));
        settings.CurveSmoothing.Subscribe(value => UpdateSetting(s => s with { CurveSmoothing = value }));
        settings.InterpolationStrength.Subscribe(value => UpdateSetting(s => s with { InterpolationStrength = value }));
        settings.StateTransitionTime.Subscribe(value => UpdateSetting(s => s with { StateTransitionTime = value }));
        settings.EnableDebugLogging.Subscribe(OnDebugLoggingChanged);
    }

    private void UpdateSetting(Func<LeashSettings, LeashSettings> updateFunc)
    {
        try
        {
            lock (_settingsLock)
            {
                _settings = updateFunc(_settings);
            }
        }
        catch (Exception e)
        {
            Log($"Error updating settings: {e.Message}");
        }
    }

    private LeashDirection ParseLeashDirection(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            return LeashDirection.None;

        var parts = parameterName.Split('_');
        if (parts.Length >= 2 && Enum.TryParse<LeashDirection>(parts[1], true, out var direction))
        {
            return direction;
        }
        return LeashDirection.None;
    }

    private string GetBaseParameterName(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            return string.Empty;

        var parts = parameterName.Split('_');
        return parts.Length >= 2 ? parts[0] : parameterName;
    }

    private void UpdateParameterValue(RegisteredParameter parameter)
    {
        try
        {
            switch (parameter.Lookup)
            {
                case LeashParameter.IsGrabbed:
                    var isGrabbed = parameter.GetValue<bool>();
                    if (isGrabbed != _stateManager.State.IsGrabbed)
                    {
                        _stateManager.HandleGrabbedStateChange(isGrabbed);
                    }
                    _parameterHandler.UpdateParameterValue(parameter.Name, isGrabbed ? 1f : 0f);
                    break;

                case LeashParameter.Stretch:
                    var stretch = _movementCalculator.ValidateForceValue(parameter.GetValue<float>());
                    _stateManager.UpdateStretch(stretch);
                    _parameterHandler.UpdateParameterValue(parameter.Name, stretch);
                    break;

                case LeashParameter.ZPositive:
                case LeashParameter.ZNegative:
                case LeashParameter.XPositive:
                case LeashParameter.XNegative:
                case LeashParameter.YPositive:
                case LeashParameter.YNegative:
                    var value = _movementCalculator.ValidateForceValue(parameter.GetValue<float>());
                    var forces = _stateManager.State.PositiveForces;
                    var negForces = _stateManager.State.NegativeForces;

                    switch (parameter.Lookup)
                    {
                        case LeashParameter.ZPositive: forces.Z = value; break;
                        case LeashParameter.ZNegative: negForces.Z = value; break;
                        case LeashParameter.XPositive: forces.X = value; break;
                        case LeashParameter.XNegative: negForces.X = value; break;
                        case LeashParameter.YPositive: forces.Y = value; break;
                        case LeashParameter.YNegative: negForces.Y = value; break;
                    }

                    _stateManager.UpdatePositiveForces(forces);
                    _stateManager.UpdateNegativeForces(negForces);
                    _parameterHandler.UpdateParameterValue(parameter.Name, value);

                    // Update movement state based on forces
                    var hasForces = forces != System.Numerics.Vector3.Zero || negForces != System.Numerics.Vector3.Zero;
                    _stateManager.UpdateMovementState(hasForces);
                    break;
            }

            _parameterHandler.CleanupCache(_stateManager.CurrentLeashPoint.BaseName);
        }
        catch (Exception ex)
        {
            Log($"Error updating parameter value: {ex.Message}");
        }
    }
} 