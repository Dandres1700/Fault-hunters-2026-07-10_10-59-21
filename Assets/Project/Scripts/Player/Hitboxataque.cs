using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitboxAtaque : MonoBehaviour
{
    [SerializeField] private Transform origenAtaque;
    [SerializeField] private Transform raizPropietario;
    [SerializeField] private Transform direccionReferencia;
    [SerializeField] private LayerMask capasGolpeables;
    [SerializeField, Min(0.01f)] private float radio = 0.45f;
    [SerializeField, Min(0f)] private float desplazamientoFrontal = 0.65f;
    [SerializeField, Range(4, 64)] private int capacidadDeteccion = 24;

    private readonly HashSet<UnityEngine.Object> objetivosGolpeados =
        new HashSet<UnityEngine.Object>();
    private Collider[] resultados;
    private float danoActual;
    private bool activa;

    public bool EstaActiva => activa;

    private void Awake()
    {
        origenAtaque ??= transform;
        raizPropietario ??= transform.root;
        direccionReferencia ??= raizPropietario;
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

            IRecibeImpacto impactReceiver = candidate.GetComponentInParent<IRecibeImpacto>();
            if (impactReceiver != null)
            {
                UnityEngine.Object identity = impactReceiver.IdentidadImpacto;
                if (identity == null || !objetivosGolpeados.Add(identity))
                {
                    continue;
                }

                Vector3 point = candidate.ClosestPoint(center);
                Vector3 direction = candidate.bounds.center - raizPropietario.position;
                impactReceiver.RecibirImpacto(
                    new DamageInfo(danoActual, point, direction, raizPropietario.gameObject)
                );
                continue;
            }

            IRecibeDano damageable = candidate.GetComponentInParent<IRecibeDano>();
            UnityEngine.Object fallbackIdentity = damageable as UnityEngine.Object;
            if (damageable != null && fallbackIdentity != null &&
                objetivosGolpeados.Add(fallbackIdentity))
            {
                damageable.RecibirDano(danoActual);
            }
        }
    }

    private bool IsOwnedByPlayer(Transform candidate)
    {
        return raizPropietario != null &&
               (candidate == raizPropietario || candidate.IsChildOf(raizPropietario));
    }

    private Vector3 GetAttackCenter()
    {
        Vector3 forward = direccionReferencia != null
            ? direccionReferencia.forward
            : origenAtaque.forward;
        return origenAtaque.position + forward * desplazamientoFrontal;
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
        Transform direction = direccionReferencia != null ? direccionReferencia : origin;
        Gizmos.DrawWireSphere(origin.position + direction.forward * desplazamientoFrontal, radio);
    }
}

public interface IRecibeDano
{
    void RecibirDano(float cantidad);
}
