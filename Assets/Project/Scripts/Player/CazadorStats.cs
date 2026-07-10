using System;
using UnityEngine;

/// <summary>
/// Maneja vida y stamina del Cazador (jugador).
/// Otros sistemas (UI, combate, controller) se suscriben a los eventos
/// en lugar de leer los valores directamente cada frame.
/// </summary>
public class CazadorStats : MonoBehaviour
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

    public bool EstaVivo => vidaActual > 0f;
    public bool EsInvulnerable => timerInvulnerabilidad > 0f;
    public float VidaActual => vidaActual;
    public float VidaMaxima => vidaMaxima;
    public float StaminaActual => staminaActual;
    public float StaminaMaxima => staminaMaxima;

    // Eventos para UI y otros sistemas
    public event Action<float, float> OnVidaCambiada;       // (actual, maxima)
    public event Action<float, float> OnStaminaCambiada;    // (actual, maxima)
    public event Action OnMuerte;
    public event Action<float> OnDanoRecibido;               // cantidad de dano

    private void Awake()
    {
        vidaActual = vidaMaxima;
        staminaActual = staminaMaxima;
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
        if (!EstaVivo || EsInvulnerable) return;

        vidaActual = Mathf.Max(0f, vidaActual - cantidad);
        timerInvulnerabilidad = duracionInvulnerabilidadAlRecibirDano;

        OnDanoRecibido?.Invoke(cantidad);
        OnVidaCambiada?.Invoke(vidaActual, vidaMaxima);

        if (vidaActual <= 0f)
        {
            OnMuerte?.Invoke();
        }
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
        OnVidaCambiada?.Invoke(vidaActual, vidaMaxima);
        OnStaminaCambiada?.Invoke(staminaActual, staminaMaxima);
    }
}