using System;
using UnityEngine;

public enum MutantLocomotionState
{
    Grounded,
    Jumping,
    Falling,
    Landing
}

public enum MutantActionState
{
    Free,
    Attacking,
    Dead
}

[DisallowMultipleComponent]
public sealed class MutantStateController : MonoBehaviour
{
    public MutantLocomotionState Locomotion { get; private set; } =
        MutantLocomotionState.Grounded;
    public MutantActionState Action { get; private set; } = MutantActionState.Free;
    public bool IsCrouching { get; private set; }
    public bool AttackLocksMovement { get; private set; }

    public bool IsGrounded => Locomotion == MutantLocomotionState.Grounded ||
                              Locomotion == MutantLocomotionState.Landing;
    public bool IsDead => Action == MutantActionState.Dead;
    public bool MovementLocked => IsDead ||
                                  (Action == MutantActionState.Attacking &&
                                   AttackLocksMovement);
    public bool CanJump => IsGrounded && !IsCrouching && Action == MutantActionState.Free;
    public bool CanSprint => IsGrounded && !IsCrouching && Action == MutantActionState.Free;
    public bool CanToggleCrouch => IsGrounded && Action == MutantActionState.Free;
    public bool CanAttack => IsGrounded && !IsCrouching && Action == MutantActionState.Free;

    public event Action<MutantLocomotionState, MutantLocomotionState>
        LocomotionChanged;

    public void SetLocomotion(MutantLocomotionState next)
    {
        if (Locomotion == next)
        {
            return;
        }

        MutantLocomotionState previous = Locomotion;
        Locomotion = next;
        LocomotionChanged?.Invoke(previous, next);
    }

    public bool TrySetCrouching(bool value)
    {
        if (!CanToggleCrouch)
        {
            return false;
        }

        IsCrouching = value;
        return true;
    }

    public bool TryBeginAttack(bool locksMovement)
    {
        if (!CanAttack)
        {
            return false;
        }

        Action = MutantActionState.Attacking;
        AttackLocksMovement = locksMovement;
        return true;
    }

    public void EndAttack()
    {
        if (Action != MutantActionState.Attacking)
        {
            return;
        }

        Action = MutantActionState.Free;
        AttackLocksMovement = false;
    }

    public void SetDead()
    {
        Action = MutantActionState.Dead;
        AttackLocksMovement = true;
    }
}
