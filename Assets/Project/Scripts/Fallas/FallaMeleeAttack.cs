using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallaMeleeAttack : MonoBehaviour, IFallaAttack
{
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private LayerMask capasAtacables = 1 << 8;
    [SerializeField, Min(0.05f)] private float radio = 1.3f;
    [SerializeField, Min(0f)] private float extensionVisual = 0.35f;
    [SerializeField] private ParticleSystem particulasAtaque;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoAtaque;

    private readonly Collider[] results = new Collider[24];
    private readonly HashSet<UnityEngine.Object> hitIdentities =
        new HashSet<UnityEngine.Object>();
    private Coroutine routine;
    private FallaCore owner;
    private Transform target;

    public bool IsRunning => routine != null;

    private void Awake()
    {
        attackOrigin ??= transform;
        audioSource ??= GetComponent<AudioSource>();
    }

    public void BeginAttack(FallaCore attackOwner, Transform attackTarget)
    {
        if (routine != null || attackOwner == null)
        {
            return;
        }
        owner = attackOwner;
        target = attackTarget;
        routine = StartCoroutine(AttackRoutine());
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

    private IEnumerator AttackRoutine()
    {
        particulasAtaque?.Play(true);
        if (sonidoAtaque != null && audioSource != null)
        {
            audioSource.PlayOneShot(sonidoAtaque);
        }

        float preparation = owner.Configuracion.PreparacionAtaque;
        Vector3 startScale = attackOrigin.localScale;
        float elapsed = 0f;
        while (elapsed < preparation)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, preparation));
            attackOrigin.localScale = Vector3.Lerp(
                startScale,
                new Vector3(startScale.x, startScale.y, startScale.z + extensionVisual),
                t
            );
            yield return null;
        }

        ApplyDamageOnce();
        attackOrigin.localScale = startScale;
        yield return new WaitForSeconds(0.12f);
        routine = null;
    }

    private void ApplyDamageOnce()
    {
        hitIdentities.Clear();
        Vector3 center = attackOrigin.position;
        if (target != null)
        {
            Vector3 direction = target.position - attackOrigin.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                center += direction.normalized * radio * 0.45f;
            }
        }

        int count = Physics.OverlapSphereNonAlloc(
            center, radio, results, capasAtacables, QueryTriggerInteraction.Collide);
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
            Vector3 point = candidate.ClosestPoint(center);
            Vector3 direction = candidate.bounds.center - owner.transform.position;
            receiver.RecibirImpacto(
                new DamageInfo(owner.DanoActual, point, direction, owner.gameObject));
        }

        // El robot puede tener su malla/attackOrigin desplazados por la escala
        // de la ciudad. Si ya alcanzo a su objetivo, el golpe no debe perderse
        // porque OverlapSphere no encontro el collider hijo del Cazador.
        if (hitIdentities.Count == 0 && target != null && owner != null)
        {
            CazadorStats hunter = target.GetComponentInParent<CazadorStats>();
            if (hunter != null)
            {
                Vector3 separation = hunter.transform.position - owner.transform.position;
                separation.y = 0f;
                float reach = Mathf.Max(owner.Configuracion.RangoAtaque,
                    owner.Configuracion.DistanciaMinimaObjetivo) + 0.4f;
                if (separation.sqrMagnitude <= reach * reach && hitIdentities.Add(hunter))
                {
                    hunter.RecibirImpacto(new DamageInfo(owner.DanoActual,
                        hunter.transform.position, separation, owner.gameObject));
                }
            }
        }
    }

    private void OnDisable() => CancelAttack();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackOrigin != null ? attackOrigin.position : transform.position,
            radio);
    }
}
