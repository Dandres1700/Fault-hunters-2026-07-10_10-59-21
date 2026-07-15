using System;
using UnityEngine;

public enum EstadoLocomocionCazador
{
    Grounded,
    Jumping,
    Falling
}

public enum EstadoPosturaCazador
{
    Standing,
    Crouching
}

public enum EstadoAccionCazador
{
    Free,
    Attacking,
    Dodging,
    Dead
}

[DisallowMultipleComponent]
public sealed class CazadorStateController : MonoBehaviour
{
    public EstadoLocomocionCazador Locomotion { get; private set; } =
        EstadoLocomocionCazador.Falling;
    public EstadoPosturaCazador Posture { get; private set; } =
        EstadoPosturaCazador.Standing;
    public EstadoAccionCazador Action { get; private set; } =
        EstadoAccionCazador.Free;
    public bool AttackLocksMovement { get; private set; }

    public bool IsGrounded => Locomotion == EstadoLocomocionCazador.Grounded;
    public bool IsCrouching => Posture == EstadoPosturaCazador.Crouching;
    public bool IsAttacking => Action == EstadoAccionCazador.Attacking;
    public bool IsDead => Action == EstadoAccionCazador.Dead;
    public bool MovementLocked => IsDead || Action == EstadoAccionCazador.Dodging ||
                                  (IsAttacking && AttackLocksMovement);
    public bool CanJump => IsGrounded && !IsCrouching && Action == EstadoAccionCazador.Free;
    public bool CanSprint => IsGrounded && !IsCrouching && Action == EstadoAccionCazador.Free;
    public bool CanToggleCrouch => IsGrounded && Action == EstadoAccionCazador.Free;
    public bool CanAttack => IsGrounded && !IsCrouching && Action == EstadoAccionCazador.Free;

    public event Action<EstadoLocomocionCazador, EstadoLocomocionCazador>
        LocomotionChanged;

    public void SetLocomotion(EstadoLocomocionCazador next)
    {
        if (Locomotion == next)
        {
            return;
        }

        EstadoLocomocionCazador previous = Locomotion;
        Locomotion = next;
        LocomotionChanged?.Invoke(previous, next);
    }

    public bool TrySetCrouching(bool crouching)
    {
        if (!CanToggleCrouch)
        {
            return false;
        }

        Posture = crouching
            ? EstadoPosturaCazador.Crouching
            : EstadoPosturaCazador.Standing;
        return true;
    }

    public bool TryBeginAttack(bool locksMovement)
    {
        if (!CanAttack)
        {
            return false;
        }

        Action = EstadoAccionCazador.Attacking;
        AttackLocksMovement = locksMovement;
        return true;
    }

    public void EndAttack()
    {
        if (Action != EstadoAccionCazador.Attacking)
        {
            return;
        }

        Action = EstadoAccionCazador.Free;
        AttackLocksMovement = false;
    }

    public void SetDead()
    {
        Action = EstadoAccionCazador.Dead;
        AttackLocksMovement = true;
    }
}
