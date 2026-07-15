using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MutantStateController))]
public sealed class MutantCombat : MonoBehaviour
{
    [Header("Fuente y referencias")]
    [SerializeField] private MonoBehaviour fuenteIntenciones;
    [SerializeField] private MutantStateController state;
    [SerializeField] private MutantAnimationController animationController;
    [SerializeField] private MutantAttackHitbox hitbox;

    [Header("Golpe principal")]
    [SerializeField, Min(0f)] private float dano = 35f;
    [SerializeField, Min(0.01f)] private float duracionAtaque = 2.966667f;
    [SerializeField, Range(0f, 1f)] private float inicioImpactoNormalizado = 0.28f;
    [SerializeField, Range(0f, 1f)] private float finImpactoNormalizado = 0.52f;
    [SerializeField, Min(0f)] private float cooldown = 0.35f;
    [SerializeField] private bool bloquearMovimiento = true;

    private IMutantIntentSource intents;
    private float attackTimer;
    private float cooldownTimer;
    private bool attacking;
    private bool hitboxActive;

    public bool EstaAtacando => attacking;

    private void Awake()
    {
        fuenteIntenciones ??= GetComponent<MutantInputReader>();
        intents = fuenteIntenciones as IMutantIntentSource;
        state ??= GetComponent<MutantStateController>();
        animationController ??= GetComponent<MutantAnimationController>();
        hitbox ??= GetComponentInChildren<MutantAttackHitbox>();
    }

    private void Update()
    {
        cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);
        if (state == null || state.IsDead || intents == null)
        {
            return;
        }

        if (intents.ConsumeAttack() && !attacking)
        {
            TryStartAttack();
        }

        if (attacking)
        {
            UpdateAttack();
        }
    }

    public void SetIntentSource(MonoBehaviour source)
    {
        fuenteIntenciones = source;
        intents = source as IMutantIntentSource;
    }

    private void TryStartAttack()
    {
        if (cooldownTimer > 0f || !state.TryBeginAttack(bloquearMovimiento))
        {
            return;
        }

        attacking = true;
        hitboxActive = false;
        attackTimer = 0f;
        hitbox?.Desactivar();
        animationController?.NotifyAttack();
    }

    private void UpdateAttack()
    {
        attackTimer += Time.deltaTime;
        float normalized = Mathf.Clamp01(attackTimer / duracionAtaque);
        bool shouldHit = normalized >= inicioImpactoNormalizado &&
                         normalized <= finImpactoNormalizado;

        if (shouldHit && !hitboxActive)
        {
            hitbox?.Activar(dano);
            hitboxActive = true;
        }
        else if (!shouldHit && hitboxActive)
        {
            hitbox?.Desactivar();
            hitboxActive = false;
        }

        if (attackTimer >= duracionAtaque)
        {
            EndAttack(true);
        }
    }

    public void EventoActivarHitbox()
    {
        if (!attacking || hitboxActive)
        {
            return;
        }

        hitbox?.Activar(dano);
        hitboxActive = true;
    }

    public void EventoDesactivarHitbox()
    {
        hitbox?.Desactivar();
        hitboxActive = false;
    }

    private void EndAttack(bool startCooldown)
    {
        hitbox?.Desactivar();
        state?.EndAttack();
        attacking = false;
        hitboxActive = false;
        if (startCooldown)
        {
            cooldownTimer = cooldown;
        }
    }

    private void OnDisable()
    {
        EndAttack(false);
    }
}
