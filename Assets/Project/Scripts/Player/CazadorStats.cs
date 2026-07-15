using System;
using UnityEngine;

/// <summary>
/// Maneja vida y stamina del Cazador (jugador).
/// Otros sistemas (UI, combate, controller) se suscriben a los eventos
/// en lugar de leer los valores directamente cada frame.
/// </summary>
public class CazadorStats : MonoBehaviour, IRecibeImpacto
{
    [Header("Vida")]
    [SerializeField] private float vidaMaxima = 100f;
    private float vidaActual;

    [Header("Stamina")]
    [SerializeField] private float staminaMaxima = 100f;
    [SerializeField] private float regeneracionStaminaPorSegundo = 15f;
    [SerializeField] private float delayAntesDeRegenerar = 0.6f;
    private float staminaActual;
    private float tiempoDesdeUltimoGasto;

    [Header("Invulnerabilidad")]
    [SerializeField] private float duracionInvulnerabilidadAlRecibirDano = 0.5f;
    private float timerInvulnerabilidad;

    [Header("Efectos")]
    [SerializeField] private DamageEffects efectosDano;
    private bool muerteNotificada;

    public bool EstaVivo => vidaActual > 0f;
    public bool EsInvulnerable => timerInvulnerabilidad > 0f;
    public float VidaActual => vidaActual;
    public float VidaMaxima => vidaMaxima;
    public float StaminaActual => staminaActual;
    public float StaminaMaxima => staminaMaxima;
    public UnityEngine.Object IdentidadImpacto => this;

    // Eventos para UI y otros sistemas
    public event Action<float, float> OnVidaCambiada;       // (actual, maxima)
    public event Action<float, float> OnStaminaCambiada;    // (actual, maxima)
    public event Action OnMuerte;
    public event Action<float> OnDanoRecibido;               // cantidad de dano
    public event Action<DamageInfo> OnImpactoRecibido;

    private void Awake()
    {
        vidaActual = vidaMaxima;
        staminaActual = staminaMaxima;
        efectosDano ??= GetComponent<DamageEffects>();
    }

    private void Update()
    {
        ActualizarInvulnerabilidad();
        ActualizarRegeneracionStamina();
    }

    private void ActualizarInvulnerabilidad()
    {
        if (timerInvulnerabilidad > 0f)
        {
            timerInvulnerabilidad -= Time.deltaTime;
        }
    }

    private void ActualizarRegeneracionStamina()
    {
        if (staminaActual >= staminaMaxima) return;

        tiempoDesdeUltimoGasto += Time.deltaTime;

        if (tiempoDesdeUltimoGasto >= delayAntesDeRegenerar)
        {
            staminaActual = Mathf.Min(staminaMaxima, staminaActual + regeneracionStaminaPorSegundo * Time.deltaTime);
            OnStaminaCambiada?.Invoke(staminaActual, staminaMaxima);
        }
    }

    public void RecibirDano(float cantidad)
    {
        RecibirImpacto(new DamageInfo(cantidad, transform.position, Vector3.zero, null));
    }

    public bool RecibirImpacto(DamageInfo impacto)
    {
        if (!EstaVivo || EsInvulnerable || impacto.Cantidad <= 0f)
        {
            return false;
        }

        vidaActual = Mathf.Max(0f, vidaActual - impacto.Cantidad);
        timerInvulnerabilidad = duracionInvulnerabilidadAlRecibirDano;

        efectosDano?.Reproducir(impacto);
        OnDanoRecibido?.Invoke(impacto.Cantidad);
        OnImpactoRecibido?.Invoke(impacto);
        OnVidaCambiada?.Invoke(vidaActual, vidaMaxima);

        if (vidaActual <= 0f && !muerteNotificada)
        {
            muerteNotificada = true;
            OnMuerte?.Invoke();
        }

        return true;
    }

    public void Curar(float cantidad)
    {
        if (!EstaVivo) return;

        vidaActual = Mathf.Min(vidaMaxima, vidaActual + cantidad);
        OnVidaCambiada?.Invoke(vidaActual, vidaMaxima);
    }

    /// <summary>
    /// Intenta gastar stamina. Devuelve true si habia suficiente y se gasto.
    /// </summary>
    public bool IntentarGastarStamina(float cantidad)
    {
        if (staminaActual < cantidad) return false;

        staminaActual -= cantidad;
        tiempoDesdeUltimoGasto = 0f;
        OnStaminaCambiada?.Invoke(staminaActual, staminaMaxima);
        return true;
    }

    public void ReiniciarStats()
    {
        vidaActual = vidaMaxima;
        staminaActual = staminaMaxima;
        timerInvulnerabilidad = 0f;
        muerteNotificada = false;
        OnVidaCambiada?.Invoke(vidaActual, vidaMaxima);
        OnStaminaCambiada?.Invoke(staminaActual, staminaMaxima);
    }
}
