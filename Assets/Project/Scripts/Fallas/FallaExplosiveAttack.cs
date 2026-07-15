using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallaExplosiveAttack : MonoBehaviour, IFallaAttack
{
    [SerializeField] private LayerMask capasAtacables = 1 << 8;
    [SerializeField, Min(0.1f)] private float tiempoExplosion = 1.6f;
    [SerializeField, Min(0.1f)] private float radioExplosion = 3.25f;
    [SerializeField, Range(4, 64)] private int capacidadDeteccion = 32;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private ParticleSystem particulasAdvertencia;
    [SerializeField] private ParticleSystem particulasExplosion;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoAdvertencia;
    [SerializeField] private AudioClip sonidoExplosion;

    private readonly HashSet<UnityEngine.Object> hitIdentities =
        new HashSet<UnityEngine.Object>();
    private Collider[] results;
    private Coroutine routine;
    private FallaCore owner;

    public bool IsRunning => routine != null;

    private void Awake()
    {
        visualRoot ??= transform;
        audioSource ??= GetComponent<AudioSource>();
        results = new Collider[Mathf.Max(4, capacidadDeteccion)];
    }

    public void BeginAttack(FallaCore attackOwner, Transform target)
    {
        if (routine != null || attackOwner == null)
        {
            return;
        }
        owner = attackOwner;
        routine = StartCoroutine(ExplosionRoutine());
    }

    public void CancelAttack()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        hitIdentities.Clear();
    }

    private IEnumerator ExplosionRoutine()
    {
        particulasAdvertencia?.Play(true);
        if (sonidoAdvertencia != null && audioSource != null)
        {
            audioSource.PlayOneShot(sonidoAdvertencia);
        }

        Vector3 initialScale = visualRoot.localScale;
        float elapsed = 0f;
        while (elapsed < tiempoExplosion)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / tiempoExplosion);
            float pulseSpeed = Mathf.Lerp(5f, 18f, normalized);
            float pulse = 1f + Mathf.Sin(elapsed * pulseSpeed) *
                Mathf.Lerp(0.05f, 0.22f, normalized);
            visualRoot.localScale = initialScale * pulse;
            yield return null;
        }

        ApplyExplosionDamage();
        particulasAdvertencia?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        particulasExplosion?.Play(true);
        if (sonidoExplosion != null && audioSource != null)
        {
            audioSource.PlayOneShot(sonidoExplosion);
        }
        visualRoot.localScale = initialScale;
        routine = null;
        owner.KillImmediately();
    }

    private void ApplyExplosionDamage()
    {
        hitIdentities.Clear();
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radioExplosion,
            results,
            capasAtacables,
            QueryTriggerInteraction.Collide
        );
        for (int index = 0; index < count; index++)
        {
            Collider candidate = results[index];
            results[index] = null;
            if (candidate == null)
            {
                continue;
            }
            if (!CombatTargeting.TryGetCazador(candidate, out IRecibeImpacto receiver) ||
                receiver.IdentidadImpacto == null ||
                !hitIdentities.Add(receiver.IdentidadImpacto))
            {
                continue;
            }
            Vector3 direction = candidate.bounds.center - transform.position;
            receiver.RecibirImpacto(new DamageInfo(
                owner.DanoActual,
                candidate.ClosestPoint(transform.position),
                direction,
                owner.gameObject
            ));
        }
    }

    private void OnDisable() => CancelAttack();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0f);
        Gizmos.DrawWireSphere(transform.position, radioExplosion);
    }
}
