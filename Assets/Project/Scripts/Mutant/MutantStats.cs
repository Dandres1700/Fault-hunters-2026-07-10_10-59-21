using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MutantStats : MonoBehaviour, IRecibeDano
{
    [SerializeField, Min(1f)] private float vidaMaxima = 500f;
    [SerializeField, Min(0f)] private float invulnerabilidadTrasImpacto = 0.15f;
    [SerializeField] private MutantStateController state;

    private float vidaActual;
    private float invulnerabilityTimer;

    public float VidaActual => vidaActual;
    public float VidaMaxima => vidaMaxima;
    public bool EstaVivo => vidaActual > 0f;

    public event Action<float, float> VidaCambiada;
    public event Action<float> DanoRecibido;
    public event Action Murio;

    private void Awake()
    {
        state ??= GetComponent<MutantStateController>();
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
        if (!EstaVivo || invulnerabilityTimer > 0f || cantidad <= 0f)
        {
            return;
        }

        vidaActual = Mathf.Max(0f, vidaActual - cantidad);
        invulnerabilityTimer = invulnerabilidadTrasImpacto;
        DanoRecibido?.Invoke(cantidad);
        VidaCambiada?.Invoke(vidaActual, vidaMaxima);

        if (vidaActual <= 0f)
        {
            state?.SetDead();
            Murio?.Invoke();
        }
    }

    public void Reiniciar()
    {
        vidaActual = vidaMaxima;
        invulnerabilityTimer = 0f;
        VidaCambiada?.Invoke(vidaActual, vidaMaxima);
    }
}
