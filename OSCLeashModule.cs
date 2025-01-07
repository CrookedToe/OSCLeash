using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using VRCOSC.Modules.OSCLeash.Constants;
using VRCOSC.Modules.OSCLeash.Enums;
using VRCOSC.Modules.OSCLeash.Models;
using VRCOSC.Modules.OSCLeash.Utils;

namespace VRCOSC.Modules.OSCLeash;

[ModuleTitle("OSC Leash")]
[ModuleDescription("Allows for controlling avatar movement with parameters")]
[ModuleType(ModuleType.Generic)]
public class OSCLeashModule : Module, IAsyncDisposable
{
    private readonly ReaderWriterLockSlim _parameterLock = new();
    private readonly ConcurrentQueue<RegisteredParameter> _parameterQueue = new();
    private readonly object _settingsLock = new();
    private readonly Queue<Action> _stateUpdates = new();
    private bool _isDisposed;

    private LeashState _state;
    private LeashSettings _settings;
    private LeashPoint _currentLeashPoint;

    private LeashDirection ParseLeashDirection(string parameterName)
    {
        var parts = parameterName.Split('_');
        if (parts.Length >= 2 && Enum.TryParse<LeashDirection>(parts[1], true, out var direction))
        {
            return direction;
        }
        return LeashDirection.None;
    }

    private string GetBaseParameterName(string parameterName)
    {
        var parts = parameterName.Split('_');
        return parts.Length >= 2 ? parts[0] : parameterName;
    }

    protected override void OnPreLoad()
    {
        CreateCustomSetting(LeashSetting.Settings, new OSCLeashModuleSettings());
        var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
        var baseName = settings.LeashVariable.Value;

        Log("=== Registering Parameters ===");
        
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
    }

    private void OnVariableNameChanged(string newName)
    {
        OnPreLoad();
    }

    private void OnDirectionChanged(string newDirection)
    {
        if (!Enum.TryParse<LeashDirection>(newDirection, true, out var direction))
        {
            direction = LeashDirection.North;
        }

        _parameterLock.EnterWriteLock();
        try
        {
            _currentLeashPoint = new LeashPoint 
            { 
                Direction = direction,
                BaseName = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings).LeashVariable.Value,
                IsActive = true
            };
        }
        finally
        {
            _parameterLock.ExitWriteLock();
        }
    }

    protected override void OnPostLoad()
    {
        var settings = GetSetting<OSCLeashModuleSettings>(LeashSetting.Settings);
        
        // Initialize leash point with current direction
        if (Enum.TryParse<LeashDirection>(settings.LeashDirection.Value, true, out var direction))
        {
            _currentLeashPoint = new LeashPoint
            {
                Direction = direction,
                BaseName = settings.LeashVariable.Value,
                IsActive = true
            };
        }
        else
        {
            _currentLeashPoint = new LeashPoint
            {
                Direction = LeashDirection.North,
                BaseName = settings.LeashVariable.Value,
                IsActive = true
            };
        }

        _settings = LeashSettings.FromSettings(settings);
        SubscribeToSettingChanges(settings);
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

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        try
        {
            if (_parameterQueue.Count < LeashConstants.MAX_QUEUE_SIZE)
            {
                _parameterQueue.Enqueue(parameter);
                ProcessParameterQueue();
            }
            else
            {
                Log("Parameter queue overflow - dropping parameter");
            }
        }
        catch (Exception e)
        {
            Log($"Error processing parameter: {e.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessParameterQueue()
    {
        var processedCount = 0;
        var parameters = new List<RegisteredParameter>();

        while (processedCount < LeashConstants.BATCH_SIZE && _parameterQueue.TryDequeue(out var parameter))
        {
            parameters.Add(parameter);
            processedCount++;
        }

        if (parameters.Count > 0)
        {
            _parameterLock.EnterWriteLock();
            try
            {
                foreach (var parameter in parameters)
                {
                    HandleParameterNoLock(parameter);
                }
            }
            finally
            {
                _parameterLock.ExitWriteLock();
            }

            if (_state.IsGrabbed && _state.IsMoving)
            {
                UpdateMovement();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleParameterNoLock(RegisteredParameter parameter)
    {
        var paramName = parameter.Name;
        var direction = ParseLeashDirection(paramName);
        var baseName = GetBaseParameterName(paramName);

        if (direction != LeashDirection.None && 
            (_currentLeashPoint.Direction == LeashDirection.None || 
             _currentLeashPoint.BaseName != baseName))
        {
            _currentLeashPoint = new LeashPoint 
            { 
                Direction = direction,
                BaseName = baseName,
                IsActive = true
            };
        }

        switch (parameter.Lookup)
        {
            case LeashParameter.IsGrabbed:
                var isGrabbed = parameter.GetValue<bool>();
                if (isGrabbed != _state.IsGrabbed)
                {
                    HandleGrabbedStateChangeNoLock(isGrabbed);
                }
                break;

            default:
                UpdateParameterValueNoLock(parameter);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleGrabbedStateChangeNoLock(bool isGrabbed)
    {
        if (isGrabbed)
        {
            _state.SetState(LeashStateFlags.Grabbed);
            _state.SetState(LeashStateFlags.Moving);
        }
        else
        {
            _state.ClearState(LeashStateFlags.Grabbed);
            _state.ClearState(LeashStateFlags.Moving);
            _state.PositiveForces = Vector3.Zero;
            _state.NegativeForces = Vector3.Zero;
            _state.CurrentMovement = Vector3.Zero;
        }
        SendMovementValues(Vector3.Zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateParameterValueNoLock(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case LeashParameter.Stretch:
                _state.Stretch = parameter.GetValue<float>();
                break;
            case LeashParameter.ZPositive:
                _state.PositiveForces.Z = parameter.GetValue<float>();
                break;
            case LeashParameter.ZNegative:
                _state.NegativeForces.Z = parameter.GetValue<float>();
                break;
            case LeashParameter.XPositive:
                _state.PositiveForces.X = parameter.GetValue<float>();
                break;
            case LeashParameter.XNegative:
                _state.NegativeForces.X = parameter.GetValue<float>();
                break;
            case LeashParameter.YPositive:
                _state.PositiveForces.Y = parameter.GetValue<float>();
                break;
            case LeashParameter.YNegative:
                _state.NegativeForces.Y = parameter.GetValue<float>();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMovement()
    {
        var currentTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
        var deltaTime = Math.Max(currentTime - _state.LastUpdateTime, LeashConstants.MovementLimits.MinimumTimeStep);
        _state.LastUpdateTime = currentTime;

        LeashSettings currentSettings;
        lock (_settingsLock)
        {
            currentSettings = _settings;
        }

        var forces = CalculateForces(currentSettings, deltaTime);
        var strength = forces.Length();
        _state.CurrentStrength = strength;
        
        if (strength >= currentSettings.RunDeadzone)
        {
            _state.SetState(LeashStateFlags.Running);
        }
        else
        {
            _state.ClearState(LeashStateFlags.Running);
        }

        if (currentSettings.TurningEnabled)
        {
            var turnAmount = CalculateTurnAmount(forces);
            turnAmount = ProcessTurning(turnAmount, currentSettings, deltaTime);
            
            if (currentSettings.EnableSafetyLimits)
            {
                turnAmount = Math.Clamp(turnAmount, -currentSettings.MaxTurnRate * deltaTime, currentSettings.MaxTurnRate * deltaTime);
            }
            
            _state.SetState(LeashStateFlags.Turning);
            _state.CurrentTurnAngle = turnAmount;
        }
        else
        {
            _state.ClearState(LeashStateFlags.Turning);
            _state.CurrentTurnAngle = 0;
        }
        
        SendMovementValues(forces);
    }

    private Vector3 CalculateForces(LeashSettings settings, float deltaTime)
    {
        var forces = _state.PositiveForces - _state.NegativeForces;
        forces *= _state.Stretch * settings.StrengthMultiplier;
        
        if (settings.EnableSafetyLimits)
        {
            forces = Vector3.Clamp(forces, new Vector3(-settings.MaxVelocity), new Vector3(settings.MaxVelocity));
        }
        
        return forces;
    }

    private float ProcessTurning(float turnInput, LeashSettings settings, float deltaTime)
    {
        _state.TargetTurnAngle = turnInput;
        _state.TurningMomentum = MathHelper.LerpAngle(
            _state.TurningMomentum,
            (turnInput - _state.CurrentTurnAngle) * settings.TurningMomentum,
            deltaTime
        );

        var turnDelta = MathHelper.LerpAngle(
            _state.CurrentTurnAngle,
            _state.TargetTurnAngle,
            settings.SmoothTurningSpeed * deltaTime
        ) + _state.TurningMomentum;
        
        _state.CurrentTurnAngle = turnDelta;
        return _state.CurrentTurnAngle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateTurnAmount(Vector3 forces)
    {
        if (!_settings.TurningEnabled || forces.Length() < _settings.TurningDeadzone)
            return 0f;

        var angle = (float)Math.Atan2(forces.X, forces.Z);
        var turnAmount = angle * _settings.TurningMultiplier;
        
        turnAmount = _currentLeashPoint.Direction switch
        {
            LeashDirection.North => turnAmount,
            LeashDirection.South => -turnAmount,
            LeashDirection.East => turnAmount - MathF.PI / 2,
            LeashDirection.West => turnAmount + MathF.PI / 2,
            _ => turnAmount
        };
        
        return turnAmount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SendMovementValues(Vector3 forces)
    {
        var horizontal = forces.X;
        var vertical = forces.Z;
        var turnAmount = CalculateTurnAmount(forces);
        
        try
        {
            var player = GetPlayer();
            if (player == null)
            {
                SendParameter(LeashParameter.Vertical, vertical);
                SendParameter(LeashParameter.Horizontal, horizontal);
                if (_settings.TurningEnabled)
                {
                    SendParameter(LeashParameter.LookHorizontal, turnAmount);
                }
                SendParameter(LeashParameter.Run, _state.IsRunning);
                return;
            }
            
            player.MoveVertical(vertical);
            player.MoveHorizontal(horizontal);
            if (_settings.TurningEnabled)
            {
                player.LookHorizontal(turnAmount);
            }
            if (_state.IsRunning)
            {
                player.Run();
            }
            else
            {
                player.StopRun();
            }
        }
        catch (Exception e)
        {
            Log($"Error sending movement values: {e.Message}");
            SendParameter(LeashParameter.Vertical, vertical);
            SendParameter(LeashParameter.Horizontal, horizontal);
            if (_settings.TurningEnabled)
            {
                SendParameter(LeashParameter.LookHorizontal, turnAmount);
            }
            SendParameter(LeashParameter.Run, _state.IsRunning);
        }
    }

    private void ResetMovement()
    {
        _parameterLock.EnterWriteLock();
        try
        {
            _state = new LeashState();
            SendMovementValues(Vector3.Zero);
        }
        finally
        {
            _parameterLock.ExitWriteLock();
        }
    }

    protected override void OnAvatarChange(AvatarConfig? avatarConfig)
    {
        ResetMovement();
    }

    protected override async Task OnModuleStop()
    {
        await DisposeAsync();
        await base.OnModuleStop();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;

        try
        {
            if (_parameterLock != null)
            {
                _parameterLock.Dispose();
            }

            _isDisposed = true;
        }
        catch (Exception e)
        {
            Log($"Error during async disposal: {e.Message}");
        }

        await ValueTask.CompletedTask;
    }
} 