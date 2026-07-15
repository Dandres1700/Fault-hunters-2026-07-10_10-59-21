using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitboxAtaque : MonoBehaviour
{
    [SerializeField] private Transform origenAtaque;
    [SerializeField] private Transform raizPropietario;
    [SerializeField] private LayerMask capasGolpeables;
    [SerializeField, Min(0.01f)] private float radio = 0.45f;
    [SerializeField, Min(0f)] private float desplazamientoFrontal = 0.65f;
    [SerializeField, Range(4, 64)] private int capacidadDeteccion = 24;

    private readonly HashSet<IRecibeDano> objetivosGolpeados =
        new HashSet<IRecibeDano>();
    private Collider[] resultados;
    private float danoActual;
    private bool activa;

    public bool EstaActiva => activa;

    private void Awake()
    {
        origenAtaque ??= transform;
        raizPropietario ??= transform.root;
        resultados = new Collider[Mathf.Max(4, capacidadDeteccion)];
    }

    private void Update()
    {
        if (activa)
        {
            DetectarImpactos();
        }
    }

    public void Activar(float dano)
    {
        danoActual = Mathf.Max(0f, dano);
        objetivosGolpeados.Clear();
        activa = true;
        DetectarImpactos();
    }

    public void Desactivar()
    {
        activa = false;
    }

    private void DetectarImpactos()
    {
        if (resultados == null || origenAtaque == null)
        {
            return;
        }

        Vector3 center = GetAttackCenter();
        int count = Physics.OverlapSphereNonAlloc(
            center,
            radio,
            resultados,
            capasGolpeables,
            QueryTriggerInteraction.Collide
        );

        for (int index = 0; index < count; index++)
        {
            Collider candidate = resultados[index];
            resultados[index] = null;
            if (candidate == null || IsOwnedByPlayer(candidate.transform))
            {
                continue;
            }

            IRecibeDano damageable = candidate.GetComponentInParent<IRecibeDano>();
            if (damageable == null || !objetivosGolpeados.Add(damageable))
            {
                continue;
            }

            damageable.RecibirDano(danoActual);
        }
    }

    private bool IsOwnedByPlayer(Transform candidate)
    {
        return raizPropietario != null &&
               (candidate == raizPropietario || candidate.IsChildOf(raizPropietario));
    }

    private Vector3 GetAttackCenter()
    {
        return origenAtaque.position + origenAtaque.forward * desplazamientoFrontal;
    }

    private void OnDisable()
    {
        activa = false;
        objetivosGolpeados.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = origenAtaque != null ? origenAtaque : transform;
        Gizmos.color = activa ? Color.red : new Color(1f, 0.55f, 0f);
        Gizmos.DrawWireSphere(
            origin.position + origin.forward * desplazamientoFrontal,
            radio
        );
    }
}

public interface IRecibeDano
{
    void RecibirDano(float cantidad);
}
