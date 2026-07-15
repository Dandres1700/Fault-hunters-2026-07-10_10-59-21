using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MutantStats : MonoBehaviour, IRecibeImpacto
{
    [SerializeField, Min(1f)] private float vidaMaxima = 500f;
    [SerializeField, Min(0f)] private float invulnerabilidadTrasImpacto = 0.15f;
    [SerializeField] private MutantStateController state;
    [SerializeField] private DamageEffects efectosDano;

    private float vidaActual;
    private float invulnerabilityTimer;
    private bool deathNotified;

    public float VidaActual => vidaActual;
    public float VidaMaxima => vidaMaxima;
    public bool EstaVivo => vidaActual > 0f;
    public UnityEngine.Object IdentidadImpacto => this;

    public event Action<float, float> VidaCambiada;
    public event Action<float> DanoRecibido;
    public event Action Murio;
    public event Action OnDeath;
    public event Action<DamageInfo> ImpactoRecibido;

    private void Awake()
    {
        state ??= GetComponent<MutantStateController>();
        efectosDano ??= GetComponent<DamageEffects>();
        vidaActual = vidaMaxima;
    }

    private void Update()
    {
        invulnerabilityTimer = Mathf.Max(
            0f,
            invulnerabilityTimer - Time.deltaTime
        );
    }

    public void RecibirDano(float cantidad)
    {
        RecibirImpacto(new DamageInfo(cantidad, transform.position, Vector3.zero, null));
    }

    public bool RecibirImpacto(DamageInfo impacto)
    {
        if (!EstaVivo || invulnerabilityTimer > 0f || impacto.Cantidad <= 0f)
        {
            return false;
        }

        vidaActual = Mathf.Max(0f, vidaActual - impacto.Cantidad);
        invulnerabilityTimer = invulnerabilidadTrasImpacto;
        efectosDano?.Reproducir(impacto);
        DanoRecibido?.Invoke(impacto.Cantidad);
        ImpactoRecibido?.Invoke(impacto);
        VidaCambiada?.Invoke(vidaActual, vidaMaxima);

        if (vidaActual <= 0f && !deathNotified)
        {
            deathNotified = true;
            state?.SetDead();
            Murio?.Invoke();
            OnDeath?.Invoke();
        }

        return true;
    }

    public void Reiniciar()
    {
        vidaActual = vidaMaxima;
        invulnerabilityTimer = 0f;
        deathNotified = false;
        VidaCambiada?.Invoke(vidaActual, vidaMaxima);
    }
}
