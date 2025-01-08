using System.Numerics;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using VRCOSC.Modules.OSCLeash.Enums;

namespace VRCOSC.Modules.OSCLeash.Models;

public class LeashStateManager
{
    private readonly Action<string> _logAction;
    private readonly bool _debugLogging;
    private LeashState _state;
    private LeashPoint _currentLeashPoint;
    private readonly Action<LeashParameter, object>? _sendParameter;

    public LeashStateManager(Action<string> logAction, Action<LeashParameter, object> sendParameter, bool debugLogging = false)
    {
        _logAction = logAction;
        _sendParameter = sendParameter;
        _debugLogging = debugLogging;
        _state = new LeashState();
        _currentLeashPoint = new LeashPoint
        {
            Direction = LeashDirection.None,
            BaseName = string.Empty,
            IsActive = false
        };
    }

    private void DebugLog(string message)
    {
        if (_debugLogging)
        {
            _logAction(message);
        }
    }

    public void HandleGrabbedStateChange(bool isGrabbed)
    {
        if (isGrabbed)
        {
            _state.SetState(LeashStateFlags.Grabbed);
            // Don't set moving state here, let it be set by force updates
        }
        else
        {
            _state.ClearState(LeashStateFlags.Grabbed);
            _state.ClearState(LeashStateFlags.Moving);
            _state.ClearState(LeashStateFlags.Running);
            _state.ClearState(LeashStateFlags.Turning);
            _state.PositiveForces = Vector3.Zero;
            _state.NegativeForces = Vector3.Zero;
            _state.CurrentMovement = Vector3.Zero;
            _state.CurrentTurnAngle = 0;
            _state.TargetTurnAngle = 0;
            _state.TurningMomentum = 0;
        }
    }

    public void UpdateMovementState(bool hasForces)
    {
        if (_state.IsGrabbed)
        {
            var wasMoving = _state.IsMoving;
            _state.ToggleState(LeashStateFlags.Moving, hasForces);
            
            if (wasMoving != _state.IsMoving)
            {
                DebugLog($"Movement state changed: {(_state.IsMoving ? "Started" : "Stopped")}");
            }
        }
    }

    public void UpdateRunningState(float strength, float runDeadzone)
    {
        var wasRunning = _state.IsRunning;
        _state.ToggleState(LeashStateFlags.Running, strength >= runDeadzone);
        
        if (wasRunning != _state.IsRunning)
        {
            DebugLog($"Running state changed: {(_state.IsRunning ? "Started" : "Stopped")}");
        }
    }

    public void UpdateTurningState(bool isTurning)
    {
        var wasTurning = _state.IsTurning;
        _state.ToggleState(LeashStateFlags.Turning, isTurning);
        
        if (wasTurning != _state.IsTurning)
        {
            DebugLog($"Turning state changed: {(_state.IsTurning ? "Started" : "Stopped")}");
        }
    }

    public void SendMovementValues(Vector3 forces, float turnAmount, LeashSettings settings, Player? player = null)
    {
        var horizontal = forces.X;
        var vertical = forces.Z;
        
        try
        {
            if (player == null)
            {
                _sendParameter?.Invoke(LeashParameter.Vertical, vertical);
                _sendParameter?.Invoke(LeashParameter.Horizontal, horizontal);
                if (settings.TurningEnabled)
                {
                    _sendParameter?.Invoke(LeashParameter.LookHorizontal, turnAmount);
                }
                _sendParameter?.Invoke(LeashParameter.Run, _state.IsRunning);
                return;
            }
            
            player.MoveVertical(vertical);
            player.MoveHorizontal(horizontal);
            if (settings.TurningEnabled)
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
            _logAction($"Error sending movement values: {e.Message}");
            _sendParameter?.Invoke(LeashParameter.Vertical, vertical);
            _sendParameter?.Invoke(LeashParameter.Horizontal, horizontal);
            if (settings.TurningEnabled)
            {
                _sendParameter?.Invoke(LeashParameter.LookHorizontal, turnAmount);
            }
            _sendParameter?.Invoke(LeashParameter.Run, _state.IsRunning);
        }
    }

    public void Reset()
    {
        _state = new LeashState();
    }

    public LeashState State => _state;
    public LeashPoint CurrentLeashPoint
    {
        get => _currentLeashPoint;
        set => _currentLeashPoint = value;
    }

    public void UpdateStretch(float value)
    {
        _state = _state with { Stretch = value };
    }

    public void UpdatePositiveForces(Vector3 forces)
    {
        _state.PositiveForces = forces;
    }

    public void UpdateNegativeForces(Vector3 forces)
    {
        _state.NegativeForces = forces;
    }

    public void UpdateCurrentTurnAngle(float angle)
    {
        _state.CurrentTurnAngle = angle;
    }

    public void UpdateLastUpdateTime(float time)
    {
        _state.LastUpdateTime = time;
    }

    public void UpdateCurrentStrength(float strength)
    {
        _state.CurrentStrength = strength;
    }

    public void SendParameter(LeashParameter parameter, object value)
    {
        _sendParameter?.Invoke(parameter, value);
    }
} 