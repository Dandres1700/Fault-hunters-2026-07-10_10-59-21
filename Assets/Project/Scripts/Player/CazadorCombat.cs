using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CazadorInputReader))]
[RequireComponent(typeof(CazadorStateController))]
public sealed class CazadorCombat : MonoBehaviour
{
    [System.Serializable]
    public sealed class AtaqueCombo
    {
        [Tooltip("Trigger alternativo. Vacio utiliza el parametro 'Ataque'.")]
        public string nombreAnimacion;
        [Min(0f)] public float dano = 10f;
        [Min(0.01f)] public float duracionAnimacion = 0.65f;
        [Range(0f, 1f)] public float inicioVentanaImpactoNormalizada = 0.3f;
        [Range(0f, 1f)] public float finVentanaImpactoNormalizada = 0.55f;
        [Range(0f, 1f)] public float ventanaSiguienteComboInicio = 0.55f;
        [Range(0f, 1f)] public float ventanaSiguienteComboFin = 0.85f;
    }

    [Header("Referencias")]
    [SerializeField] private CazadorInputReader input;
    [SerializeField] private CazadorStateController state;
    [SerializeField] private CazadorStats stats;
    [SerializeField] private CazadorAnimationController animationController;
    [SerializeField] private HitboxAtaque hitboxAtaque;

    [Header("Ataque")]
    [SerializeField] private AtaqueCombo[] combo = { new AtaqueCombo() };
    [SerializeField, Min(0f)] private float costoStaminaPorAtaque = 10f;
    [SerializeField, Min(0f)] private float cooldownAtaque = 0.2f;
    [SerializeField] private bool bloquearMovimientoDuranteAtaque = true;
    [SerializeField] private bool usarVentanaTemporalFallback = true;

    private int indiceComboActual = -1;
    private bool estaAtacando;
    private bool inputBufferizado;
    private bool fallbackHitboxActive;
    private float timerAtaque;
    private float timerCooldown;

    public bool EstaAtacando => estaAtacando;

    private void Awake()
    {
        input ??= GetComponent<CazadorInputReader>();
        state ??= GetComponent<CazadorStateController>();
        stats ??= GetComponent<CazadorStats>();
        animationController ??= GetComponent<CazadorAnimationController>();
        hitboxAtaque ??= GetComponentInChildren<HitboxAtaque>();
    }

    private void Update()
    {
        if (timerCooldown > 0f)
        {
            timerCooldown -= Time.deltaTime;
        }

        if (state == null || state.IsDead || input == null)
        {
            return;
        }

        if (input.ConsumeAttack())
        {
            if (estaAtacando)
            {
                inputBufferizado = true;
            }
            else
            {
                TryStartAttack(0);
            }
        }

        if (estaAtacando)
        {
            UpdateCurrentAttack();
        }
    }

    private void TryStartAttack(int index)
    {
        if (combo == null || combo.Length == 0 || index < 0 || index >= combo.Length)
        {
            return;
        }

        if (timerCooldown > 0f || stats == null ||
            !state.TryBeginAttack(bloquearMovimientoDuranteAtaque) ||
            !stats.IntentarGastarStamina(costoStaminaPorAtaque))
        {
            state.EndAttack();
            return;
        }

        indiceComboActual = index;
        estaAtacando = true;
        inputBufferizado = false;
        fallbackHitboxActive = false;
        timerAtaque = 0f;
        hitboxAtaque?.Desactivar();

        animationController?.NotifyAttack(combo[index].nombreAnimacion);
    }

    private void UpdateCurrentAttack()
    {
        AtaqueCombo attack = combo[indiceComboActual];
        timerAtaque += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(timerAtaque / attack.duracionAnimacion);

        if (usarVentanaTemporalFallback)
        {
            UpdateFallbackHitWindow(attack, normalizedTime);
        }

        bool inComboWindow = normalizedTime >= attack.ventanaSiguienteComboInicio &&
                             normalizedTime <= attack.ventanaSiguienteComboFin;
        if (inputBufferizado && inComboWindow)
        {
            int nextIndex = indiceComboActual + 1;
            if (nextIndex < combo.Length)
            {
                EndAttack(false);
                TryStartAttack(nextIndex);
                return;
            }

            inputBufferizado = false;
        }

        if (timerAtaque >= attack.duracionAnimacion)
        {
            EndAttack(true);
        }
    }

    private void UpdateFallbackHitWindow(AtaqueCombo attack, float normalizedTime)
    {
        bool shouldBeActive = normalizedTime >= attack.inicioVentanaImpactoNormalizada &&
                              normalizedTime <= attack.finVentanaImpactoNormalizada;
        if (shouldBeActive && !fallbackHitboxActive)
        {
            if (hitboxAtaque != null && !hitboxAtaque.EstaActiva)
            {
                hitboxAtaque.Activar(attack.dano);
            }

            fallbackHitboxActive = true;
        }
        else if (!shouldBeActive && fallbackHitboxActive)
        {
            hitboxAtaque?.Desactivar();
            fallbackHitboxActive = false;
        }
    }

    private void EndAttack(bool startCooldown)
    {
        hitboxAtaque?.Desactivar();
        state.EndAttack();
        estaAtacando = false;
        fallbackHitboxActive = false;
        inputBufferizado = false;
        indiceComboActual = -1;

        if (startCooldown)
        {
            timerCooldown = cooldownAtaque;
        }
    }

    public void EventoActivarHitbox()
    {
        if (!estaAtacando || indiceComboActual < 0 || hitboxAtaque == null)
        {
            return;
        }

        if (!hitboxAtaque.EstaActiva)
        {
            hitboxAtaque.Activar(combo[indiceComboActual].dano);
        }

        fallbackHitboxActive = true;
    }

    public void EventoDesactivarHitbox()
    {
        hitboxAtaque?.Desactivar();
        fallbackHitboxActive = false;
    }

    private void OnDisable()
    {
        hitboxAtaque?.Desactivar();
        state?.EndAttack();
    }
}
