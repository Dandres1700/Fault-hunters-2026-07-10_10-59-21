using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CazadorAnimationController : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Velocidad");
    private static readonly int MoveXHash = Animator.StringToHash("MovimientoX");
    private static readonly int MoveYHash = Animator.StringToHash("MovimientoY");
    private static readonly int GroundedHash = Animator.StringToHash("EnSuelo");
    private static readonly int CrouchingHash = Animator.StringToHash("Agachado");
    private static readonly int RunningHash = Animator.StringToHash("Corriendo");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VelocidadVertical");
    private static readonly int DodgingHash = Animator.StringToHash("Dasheando");
    private static readonly int JumpHash = Animator.StringToHash("Salto");
    private static readonly int LandHash = Animator.StringToHash("Aterrizar");
    private static readonly int AttackHash = Animator.StringToHash("Ataque");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int DeadHash = Animator.StringToHash("IsDead");

    [SerializeField] private Animator animator;
    [SerializeField] private CazadorController controller;
    [SerializeField] private CazadorStateController state;
    [SerializeField, Min(0f)] private float damping = 0.12f;

    private readonly HashSet<int> availableParameters = new HashSet<int>();

    private void Awake()
    {
        animator ??= GetComponentInChildren<Animator>();
        controller ??= GetComponent<CazadorController>();
        state ??= GetComponent<CazadorStateController>();
        CacheParameters();

        if (animator == null)
        {
            Debug.LogWarning(
                "CazadorAnimationController no encontro un Animator. La mecanica funcionara sin animaciones.",
                this
            );
        }
    }

    private void Update()
    {
        if (animator == null || controller == null || state == null)
        {
            return;
        }

        Vector3 localDirection = transform.InverseTransformDirection(
            controller.DireccionMovimientoMundo
        );
        SetFloat(SpeedHash, controller.VelocidadNormalizada, damping);
        SetFloat(MoveXHash, localDirection.x * controller.VelocidadNormalizada, damping);
        SetFloat(MoveYHash, localDirection.z * controller.VelocidadNormalizada, damping);
        SetFloat(VerticalVelocityHash, controller.VelocidadVertical, damping);
        SetBool(GroundedHash, state.IsGrounded);
        SetBool(CrouchingHash, state.IsCrouching);
        SetBool(RunningHash, controller.EstaCorriendo);
        SetBool(DodgingHash, controller.EstaDasheando);
        SetBool(DeadHash, state.IsDead);
    }

    public void NotifyJump()
    {
        SetTrigger(JumpHash);
    }

    public void NotifyLanding()
    {
        SetTrigger(LandHash);
    }

    public void NotifyAttack(string customTrigger = null)
    {
        int triggerHash = string.IsNullOrWhiteSpace(customTrigger)
            ? AttackHash
            : Animator.StringToHash(customTrigger);
        SetTrigger(triggerHash);
    }

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
