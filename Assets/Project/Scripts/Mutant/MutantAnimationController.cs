using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MutantAnimationController : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int CrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int LandHash = Animator.StringToHash("Land");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int DeadHash = Animator.StringToHash("IsDead");

    [SerializeField] private Animator animator;
    [SerializeField] private MutantMotor motor;
    [SerializeField] private MutantStateController state;
    [SerializeField, Min(0f)] private float damping = 0.12f;

    private readonly HashSet<int> availableParameters = new HashSet<int>();

    private void Awake()
    {
        animator ??= GetComponentInChildren<Animator>();
        motor ??= GetComponent<MutantMotor>();
        state ??= GetComponent<MutantStateController>();
        CacheParameters();

        if (animator == null)
        {
            Debug.LogWarning("El Mutant no encontro su Animator.", this);
        }
    }

    private void Update()
    {
        if (animator == null || motor == null || state == null)
        {
            return;
        }

        SetFloat(SpeedHash, motor.VelocidadNormalizada, damping);
        SetFloat(VerticalVelocityHash, motor.VelocidadVertical, damping);
        SetBool(GroundedHash, state.IsGrounded);
        SetBool(CrouchingHash, state.IsCrouching);
        SetBool(DeadHash, state.IsDead);
    }

    public void NotifyJump() => SetTrigger(JumpHash);
    public void NotifyLanding() => SetTrigger(LandHash);
    public void NotifyAttack() => SetTrigger(AttackHash);
    public void NotifyHit() => SetTrigger(HitHash);

    public void NotifyDeath()
    {
        SetBool(DeadHash, true);
        SetTrigger(DeathHash);
    }

    private void CacheParameters()
    {
        availableParameters.Clear();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            availableParameters.Add(parameter.nameHash);
        }
    }

    private void SetFloat(int hash, float value, float dampTime)
    {
        if (availableParameters.Contains(hash))
        {
            animator.SetFloat(hash, value, dampTime, Time.deltaTime);
        }
    }

    private void SetBool(int hash, bool value)
    {
        if (availableParameters.Contains(hash))
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetTrigger(int hash)
    {
        if (animator != null && availableParameters.Contains(hash))
        {
            animator.SetTrigger(hash);
        }
    }
}
