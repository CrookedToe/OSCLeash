using System.Numerics;
using VRCOSC.Modules.OSCLeash.Constants;
using VRCOSC.Modules.OSCLeash.Enums;
using VRCOSC.Modules.OSCLeash.Utils;

namespace VRCOSC.Modules.OSCLeash.Models;

public class LeashMovementCalculator
{
    private const float FORCE_EPSILON = 0.001f;
    private const float MAX_FORCE_VALUE = 1.0f;

    public Vector3 CalculateForces(LeashState state, LeashSettings settings, float deltaTime)
    {
        var forces = state.PositiveForces - state.NegativeForces;
        forces *= state.Stretch * settings.StrengthMultiplier;
        
        if (settings.EnableSafetyLimits)
        {
            forces = Vector3.Clamp(forces, new Vector3(-settings.MaxVelocity), new Vector3(settings.MaxVelocity));
        }
        
        return forces;
    }

    public float ProcessTurning(LeashState state, float turnInput, LeashSettings settings, float deltaTime)
    {
        state.TargetTurnAngle = turnInput;
        state.TurningMomentum = MathHelper.LerpAngle(
            state.TurningMomentum,
            (turnInput - state.CurrentTurnAngle) * settings.TurningMomentum,
            deltaTime
        );

        var turnDelta = MathHelper.LerpAngle(
            state.CurrentTurnAngle,
            state.TargetTurnAngle,
            settings.SmoothTurningSpeed * deltaTime
        ) + state.TurningMomentum;
        
        state.CurrentTurnAngle = turnDelta;
        return state.CurrentTurnAngle;
    }

    public float CalculateTurnAmount(Vector3 forces, LeashSettings settings, LeashDirection direction)
    {
        if (!settings.TurningEnabled || forces.Length() < settings.TurningDeadzone)
            return 0f;

        var angle = (float)Math.Atan2(forces.X, forces.Z);
        var turnAmount = angle * settings.TurningMultiplier;
        
        turnAmount = direction switch
        {
            LeashDirection.North => turnAmount,
            LeashDirection.South => -turnAmount,
            LeashDirection.East => turnAmount - MathF.PI / 2,
            LeashDirection.West => turnAmount + MathF.PI / 2,
            _ => turnAmount
        };
        
        return turnAmount;
    }

    public float ValidateForceValue(float value)
    {
        // Clamp force values between -1 and 1
        value = Math.Clamp(value, -MAX_FORCE_VALUE, MAX_FORCE_VALUE);
        
        // Apply deadzone to prevent micro-movements
        if (Math.Abs(value) < FORCE_EPSILON)
            return 0f;
            
        return value;
    }
} 