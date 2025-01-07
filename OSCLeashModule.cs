using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using VRCOSC.App.Utils;
using System.Diagnostics;
using System.Threading;
using System.Numerics;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace VRCOSC.Modules.OSCLeash;

[ModuleTitle("OSC Leash")]
[ModuleDescription("Allows for controlling avatar movement with parameters")]
[ModuleType(ModuleType.Generic)]
public class OSCLeashModule : Module
{
    private readonly ReaderWriterLockSlim _parameterLock = new();
    private readonly ConcurrentQueue<RegisteredParameter> _parameterQueue = new();
    private readonly object _settingsLock = new();
    private const int MAX_QUEUE_SIZE = 1000;
    private const float MOVEMENT_EPSILON = 0.0001f;
    private const int BATCH_SIZE = 10;

    private readonly struct MovementLimits
    {
        public float MaxAcceleration { get; init; }
        public float MaxVelocity { get; init; }
        public float MaxTurnRate { get; init; }
        public const float SafetyMargin = 0.95f;
        public const float MinimumTimeStep = 0.016f; // ~60fps
    }

    [Flags]
    private enum LeashStateFlags
    {
        None = 0,
        Grabbed = 1 << 0,
        Moving = 1 << 1,
        Turning = 1 << 2,
        Running = 1 << 3
    }
    
    // Parameter values stored in SIMD-friendly struct
    private struct LeashState
    {
        public Vector3 PositiveForces;  // x, y, z positive
        public Vector3 NegativeForces;  // x, y, z negative
        public Vector3 CurrentMovement;  // Current movement vector
        public float CurrentTurnAngle;  // Current turn angle for smooth turning
        public float TargetTurnAngle;   // Target turn angle for smooth turning
        public float TurningMomentum;   // Current turning momentum
        public float LastUpdateTime;    // Time of last update
        public float Stretch;
        public float CurrentStrength;   // Current movement strength
        public LeashStateFlags Flags;

        public bool IsGrabbed => (Flags & LeashStateFlags.Grabbed) != 0;
        public bool IsRunning => (Flags & LeashStateFlags.Running) != 0;
        public bool IsMoving => (Flags & LeashStateFlags.Moving) != 0;
        public bool IsTurning => (Flags & LeashStateFlags.Turning) != 0;

        public void SetState(LeashStateFlags state) => Flags |= state;
        public void ClearState(LeashStateFlags state) => Flags &= ~state;
        public void ToggleState(LeashStateFlags state, bool enable)
        {
            if (enable) SetState(state);
            else ClearState(state);
        }
    }

    private readonly Queue<Action> _stateUpdates = new();
    private LeashState _state;
    
    // Settings cached as readonly after load
    private readonly struct LeashSettings
    {
        public float WalkDeadzone { get; init; }
        public float RunDeadzone { get; init; }
        public float StrengthMultiplier { get; init; }
        public bool TurningEnabled { get; init; }
        public float TurningMultiplier { get; init; }
        public float TurningDeadzone { get; init; }
        public float TurningGoal { get; init; }
        public float SmoothTurningSpeed { get; init; }
        public float TurningMomentum { get; init; }
        public float UpDownCompensation { get; init; }
        public float UpDownDeadzone { get; init; }
        public bool EnableSafetyLimits { get; init; }
        public float MaxVelocity { get; init; }
        public float MaxAcceleration { get; init; }
        public float MaxTurnRate { get; init; }
        public string MovementCurveType { get; init; }
        public float CurveExponent { get; init; }
        public float CurveSmoothing { get; init; }
        public float InterpolationStrength { get; init; }
        public float StateTransitionTime { get; init; }

        public static LeashSettings FromSettings(OSCLeashModuleSettings settings)
        {
            return new LeashSettings
            {
                WalkDeadzone = settings.WalkDeadzone.Value,
                RunDeadzone = settings.RunDeadzone.Value,
                StrengthMultiplier = settings.StrengthMultiplier.Value,
                TurningEnabled = settings.TurningEnabled.Value,
                TurningMultiplier = settings.TurningMultiplier.Value,
                TurningDeadzone = settings.TurningDeadzone.Value,
                TurningGoal = settings.TurningGoal.Value,
                SmoothTurningSpeed = settings.SmoothTurningSpeed.Value,
                TurningMomentum = settings.TurningMomentum.Value,
                UpDownCompensation = settings.UpDownCompensation.Value,
                UpDownDeadzone = settings.UpDownDeadzone.Value,
                EnableSafetyLimits = settings.EnableSafetyLimits.Value,
                MaxVelocity = settings.MaxVelocity.Value,
                MaxAcceleration = settings.MaxAcceleration.Value,
                MaxTurnRate = settings.MaxTurnRate.Value,
                MovementCurveType = settings.MovementCurveType.Value,
                CurveExponent = settings.CurveExponent.Value,
                CurveSmoothing = settings.CurveSmoothing.Value,
                InterpolationStrength = settings.InterpolationStrength.Value,
                StateTransitionTime = settings.StateTransitionTime.Value
            };
        }
    }
    
    private LeashSettings _settings;

    private enum LeashDirection
    {
        None,
        North,  // Front
        South,  // Back
        East,   // Right
        West    // Left
    }

    private struct LeashPoint
    {
        public LeashDirection Direction;
        public string BaseName;
        public bool IsActive;
    }

    private LeashPoint _currentLeashPoint;

    private LeashDirection ParseLeashDirection(string parameterName)
    {
        var parts = parameterName.Split('_');
        if (parts.Length >= 2)
        {
            // Try to parse the direction part (second segment)
            if (Enum.TryParse<LeashDirection>(parts[1], true, out var direction))
            {
                return direction;
            }
        }
        return LeashDirection.None;
    }

    private string GetBaseParameterName(string parameterName)
    {
        var parts = parameterName.Split('_');
        if (parts.Length >= 2)
        {
            return parts[0];
        }
        return parameterName;
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
        // Re-register parameters with new base name
        OnPreLoad();
    }

    private void OnDirectionChanged(string newDirection)
    {
        // Parse direction, defaulting to North if invalid
        if (!Enum.TryParse<LeashDirection>(newDirection, true, out var direction))
        {
            direction = LeashDirection.North;
        }

        // Update turning behavior
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
                Direction = LeashDirection.North,  // Default to North
                BaseName = settings.LeashVariable.Value,
                IsActive = true
            };
        }

        // Cache settings in readonly struct
        _settings = LeashSettings.FromSettings(settings);

        // Subscribe to setting changes
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
            if (_parameterQueue.Count < MAX_QUEUE_SIZE)
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

        // First, collect parameters from the queue
        while (processedCount < BATCH_SIZE && _parameterQueue.TryDequeue(out var parameter))
        {
            parameters.Add(parameter);
            processedCount++;
        }

        if (parameters.Count > 0)
        {
            // Then process all parameters in a single write lock
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

            // Check if we need to update movement
            if (_state.IsGrabbed && _state.IsMoving)
            {
                UpdateMovement();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleParameterNoLock(RegisteredParameter parameter)
    {
        // Extract base name and direction from parameter
        var paramName = parameter.Name;
        var direction = ParseLeashDirection(paramName);
        var baseName = GetBaseParameterName(paramName);

        // Update current leash point if this is a new direction
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
        var deltaTime = Math.Max(currentTime - _state.LastUpdateTime, MovementLimits.MinimumTimeStep);
        _state.LastUpdateTime = currentTime;

        LeashSettings currentSettings;
        lock (_settingsLock)
        {
            currentSettings = _settings;
        }

        // Calculate forces
        var forces = CalculateForces(currentSettings, deltaTime);
        
        // Update movement state
        var strength = forces.Length();
        _state.CurrentStrength = strength;
        
        // Update running state based on strength
        if (strength >= currentSettings.RunDeadzone)
        {
            _state.SetState(LeashStateFlags.Running);
        }
        else
        {
            _state.ClearState(LeashStateFlags.Running);
        }

        // Process turning if enabled
        if (currentSettings.TurningEnabled)
        {
            var turnAmount = CalculateTurnAmount(forces);
            turnAmount = ProcessTurning(turnAmount, currentSettings, deltaTime);
            
            // Apply safety limits if enabled
            if (currentSettings.EnableSafetyLimits)
            {
                turnAmount = Math.Clamp(turnAmount, -currentSettings.MaxTurnRate * deltaTime, currentSettings.MaxTurnRate * deltaTime);
            }
            
            // Update turning state
            _state.SetState(LeashStateFlags.Turning);
            _state.CurrentTurnAngle = turnAmount;
        }
        else
        {
            _state.ClearState(LeashStateFlags.Turning);
            _state.CurrentTurnAngle = 0;
        }
        
        // Send movement values
        SendMovementValues(forces);
    }

    private Vector3 CalculateForces(LeashSettings settings, float deltaTime)
    {
        var forces = _state.PositiveForces - _state.NegativeForces;
        forces *= _state.Stretch * settings.StrengthMultiplier;
        
        // Apply safety limits if enabled
        if (settings.EnableSafetyLimits)
        {
            forces = Vector3.Clamp(forces, new Vector3(-settings.MaxVelocity), new Vector3(settings.MaxVelocity));
        }
        
        return forces;
    }

    private float ProcessTurning(float turnInput, LeashSettings settings, float deltaTime)
    {
        // Update target angle
        _state.TargetTurnAngle = turnInput;

        // Apply turning momentum
        _state.TurningMomentum = MathHelper.LerpAngle(
            _state.TurningMomentum,
            (turnInput - _state.CurrentTurnAngle) * settings.TurningMomentum,
            deltaTime
        );

        // Smoothly interpolate to target
        var turnDelta = MathHelper.LerpAngle(
            _state.CurrentTurnAngle,
            _state.TargetTurnAngle,
            settings.SmoothTurningSpeed * deltaTime
        ) + _state.TurningMomentum;
        
        _state.CurrentTurnAngle = turnDelta;
        return _state.CurrentTurnAngle;
    }

    private void UpdateState(float vertical, float horizontal, float turn)
    {
        bool exceedsWalkDeadzone = Math.Abs(vertical) > _settings.WalkDeadzone || 
                                Math.Abs(horizontal) > _settings.WalkDeadzone;
        bool exceedsRunDeadzone = Math.Abs(vertical) > _settings.RunDeadzone || 
                                Math.Abs(horizontal) > _settings.RunDeadzone;

        if (!exceedsWalkDeadzone)
        {
            if (_state.CurrentMovement != Vector3.Zero)
            {
                ResetMovementState();
            }
            return;
        }

        var newMovement = new Vector3(horizontal, 0, vertical);
        // Only send values if they've changed significantly
        if (Math.Abs(_state.CurrentMovement.Z - vertical) > MOVEMENT_EPSILON ||
            Math.Abs(_state.CurrentMovement.X - horizontal) > MOVEMENT_EPSILON)
        {
            _state.CurrentMovement = newMovement;
            SendMovementValues(newMovement);
        }
    }

    private void ResetMovementState()
    {
        _state.CurrentMovement = Vector3.Zero;
        _state.CurrentTurnAngle = 0;
        _state.TargetTurnAngle = 0;
        _state.TurningMomentum = 0;
        SendMovementValues(Vector3.Zero);
    }

    private float CalculateTurnAngle(float horizontalMovement)
    {
        LeashSettings currentSettings;
        lock (_settingsLock)
        {
            currentSettings = _settings;
        }

        // Adjust turning based on leash direction
        var adjustedMovement = _currentLeashPoint.Direction switch
        {
            LeashDirection.North => horizontalMovement,        // Normal turning
            LeashDirection.South => -horizontalMovement,       // Invert turning
            LeashDirection.East => horizontalMovement * 0.5f,  // Reduced turning from side
            LeashDirection.West => horizontalMovement * 0.5f,  // Reduced turning from side
            _ => horizontalMovement
        };

        // Return the adjusted movement directly (no need to multiply by turning goal)
        return Clamp(adjustedMovement);
    }

    private static class MathHelper
    {
        public static float LerpAngle(float start, float end, float amount)
        {
            float difference = end - start;
            while (difference < -180)
                difference += 360;
            while (difference > 180)
                difference -= 360;
            return start + difference * amount;
        }

        public static float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * amount;
        }
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
            // Fall back to parameter sending on error
            SendParameter(LeashParameter.Vertical, vertical);
            SendParameter(LeashParameter.Horizontal, horizontal);
            if (_settings.TurningEnabled)
            {
                SendParameter(LeashParameter.LookHorizontal, turnAmount);
            }
            SendParameter(LeashParameter.Run, _state.IsRunning);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateTurnAmount(Vector3 forces)
    {
        if (!_settings.TurningEnabled || forces.Length() < _settings.TurningDeadzone)
            return 0f;

        var angle = (float)Math.Atan2(forces.X, forces.Z);
        var turnAmount = angle * _settings.TurningMultiplier;
        
        // Apply direction-based turning
        switch (_currentLeashPoint.Direction)
        {
            case LeashDirection.North:
                // Default behavior, no modification needed
                break;
            case LeashDirection.South:
                turnAmount = -turnAmount;
                break;
            case LeashDirection.East:
                turnAmount = turnAmount - MathF.PI / 2;
                break;
            case LeashDirection.West:
                turnAmount = turnAmount + MathF.PI / 2;
                break;
        }
        
        return turnAmount;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp(float n)
    {
        if (float.IsNaN(n) || float.IsInfinity(n))
        {
            return 0.0f;
        }
        return Math.Max(-1.0f, Math.Min(n, 1.0f));
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

    private void HandleGrabbedStateChange(bool isGrabbed)
    {
        if (!isGrabbed)
        {
            ResetMovementState();
        }
        _parameterLock.EnterWriteLock();
        try
        {
            _state.ToggleState(LeashStateFlags.Grabbed, isGrabbed);
        }
        finally
        {
            _parameterLock.ExitWriteLock();
        }
    }

    private void ProcessForces(Vector3 forces, LeashSettings settings)
    {
        // Calculate vertical and horizontal components
        var vertical = forces.Z;
        var horizontal = forces.X;
        
        // Calculate turn amount
        var turn = CalculateTurnAmount(forces);
        
        // Send the movement values
        SendMovementValues(forces);
    }

    private void OnAvatarChange(string avatarId)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 ApplyMovementEnhancements(Vector3 forces, LeashSettings settings, float deltaTime)
    {
        var resultForces = forces;

        // Apply movement curve
        resultForces = ApplyMovementCurve(resultForces, settings);

        // Apply state interpolation
        resultForces = InterpolateMovement(resultForces, settings, deltaTime);

        return resultForces;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 ApplyMovementCurve(Vector3 forces, LeashSettings settings)
    {
        var magnitude = forces.Length();
        if (magnitude < MOVEMENT_EPSILON) return forces;

        var curvedMagnitude = settings.MovementCurveType switch
        {
            "Quadratic" => magnitude * magnitude,
            "Cubic" => magnitude * magnitude * magnitude,
            "Exponential" => (float)Math.Pow(magnitude, settings.CurveExponent),
            _ => magnitude // Linear
        };

        // Apply curve smoothing
        curvedMagnitude = MathHelper.Lerp(magnitude, curvedMagnitude, settings.CurveSmoothing);

        // Maintain direction while adjusting magnitude
        return Vector3.Normalize(forces) * curvedMagnitude;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 InterpolateMovement(Vector3 forces, LeashSettings settings, float deltaTime)
    {
        var targetForces = forces;
        var currentForces = _state.CurrentMovement;
        
        // Calculate interpolation factor based on transition time
        var alpha = Math.Min(deltaTime / Math.Max(settings.StateTransitionTime, 0.001f), 1.0f);
        alpha *= settings.InterpolationStrength;

        // Interpolate between current and target forces
        return Vector3.Lerp(currentForces, targetForces, alpha);
    }

    protected override async Task OnModuleStop()
    {
        try
        {
            // Clean up resources
            _parameterLock?.Dispose();
            await base.OnModuleStop();
        }
        catch (Exception e)
        {
            Log($"Error stopping module: {e.Message}");
        }
    }
} 