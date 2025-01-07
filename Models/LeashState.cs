using System.Numerics;

namespace VRCOSC.Modules.OSCLeash.Models;

[Flags]
public enum LeashStateFlags
{
    None = 0,
    Grabbed = 1 << 0,
    Moving = 1 << 1,
    Turning = 1 << 2,
    Running = 1 << 3
}

// Parameter values stored in SIMD-friendly struct
public struct LeashState
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