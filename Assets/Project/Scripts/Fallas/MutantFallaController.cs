using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MutantStats))]
public sealed class MutantFallaController : MonoBehaviour
{
    [System.Serializable]
    public sealed class PhaseRule
    {
        [Tooltip("La regla se activa cuando la vida normalizada baja de este valor.")]
        [Range(0f, 1f)] public float umbralVida = 1f;
        [Min(1)] public int maximoActivas = 2;
        [Min(0.2f)] public float intervaloInvocacion = 8f;
        [Min(0.1f)] public float multiplicadorPoder = 1f;
        public GameObject[] prefabsPermitidos;
    }

    [Header("Control")]
    [SerializeField] private bool invocacionAutomatica = true;
    [SerializeField] private Transform objetivo;
    [SerializeField] private Transform[] puntosAparicion;
    [SerializeField] private PhaseRule[] fases = { new PhaseRule() };

    [Header("Validacion espacial")]
    [SerializeField, Min(0f)] private float radioAparicion = 2f;
    [SerializeField, Min(0.1f)] private float radioLibre = 0.7f;
    [SerializeField, Min(0f)] private float distanciaMinimaObjetivo = 3f;
    [SerializeField, Range(1, 20)] private int intentosPosicion = 10;
    [SerializeField] private LayerMask capasSuelo = 1 << 9;
    [SerializeField] private LayerMask capasBloqueo = ~0;

    private readonly List<FallaCore> active = new List<FallaCore>();
    private MutantStats stats;
    private float spawnTimer;
    private bool stopped;

    public int CantidadActiva => active.Count;

    private void Awake()
    {
        stats = GetComponent<MutantStats>();
        ValidatePhaseOrder();
    }

    private void OnEnable()
    {
        stopped = false;
        spawnTimer = 1f;
        if (stats != null)
        {
            stats.OnDeath += StopAllSpawning;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnDeath -= StopAllSpawning;
        }
    }

    private void Update()
    {
        if (!invocacionAutomatica || stopped || stats == null || !stats.EstaVivo)
        {
            return;
        }

        PhaseRule phase = GetCurrentPhase();
        if (phase == null)
        {
            return;
        }
        CleanupReferences();
        if (active.Count >= phase.maximoActivas)
        {
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = phase.intervaloInvocacion;
            TrySummon(phase);
        }
    }

    public bool InvokeOne()
    {
        return TrySummon(GetCurrentPhase());
    }

    public void RegisterExisting(FallaCore falla, bool activateAgainstTarget = true)
    {
        if (falla == null || active.Contains(falla))
        {
            return;
        }
        falla.Removida += OnFallaRemoved;
        active.Add(falla);
        if (activateAgainstTarget)
        {
            falla.SetTarget(objetivo);
        }
    }

    public void StrengthenActive(float multiplier)
    {
        CleanupReferences();
        foreach (FallaCore falla in active)
        {
            falla?.ApplyPowerMultiplier(multiplier);
        }
    }

    public void StopAllSpawning()
    {
        stopped = true;
    }

    public void RemoveAllFallas()
    {
        stopped = true;
        for (int index = active.Count - 1; index >= 0; index--)
        {
            if (active[index] != null)
            {
                active[index].KillImmediately();
            }
        }
    }

    private bool TrySummon(PhaseRule phase)
    {
        CleanupReferences();
        if (phase == null || active.Count >= phase.maximoActivas ||
            phase.prefabsPermitidos == null || phase.prefabsPermitidos.Length == 0)
        {
            return false;
        }

        GameObject prefab = phase.prefabsPermitidos[Random.Range(0, phase.prefabsPermitidos.Length)];
        if (prefab == null || prefab.GetComponent<FallaCore>() == null)
        {
            Debug.LogWarning("MutantFallaController contiene un prefab de Falla invalido.", this);
            return false;
        }

        Transform point = puntosAparicion != null && puntosAparicion.Length > 0
            ? puntosAparicion[Random.Range(0, puntosAparicion.Length)]
            : transform;
        if (point == null)
        {
            point = transform;
        }

        if (!FallaSpawnUtility.TryFindValidPosition(
                point.position,
                radioAparicion,
                radioLibre,
                distanciaMinimaObjetivo,
                objetivo,
                capasSuelo,
                capasBloqueo,
                intentosPosicion,
                out Vector3 position))
        {
            return false;
        }

        FallaCore spawned = Instantiate(prefab, position, Quaternion.identity)
            .GetComponent<FallaCore>();
        spawned.ApplyPowerMultiplier(phase.multiplicadorPoder);
        RegisterExisting(spawned);
        return true;
    }

    private PhaseRule GetCurrentPhase()
    {
        if (fases == null || fases.Length == 0 || stats == null)
        {
            return null;
        }
        float normalizedHealth = stats.VidaMaxima > 0f
            ? stats.VidaActual / stats.VidaMaxima
            : 0f;
        PhaseRule selected = fases[0];
        foreach (PhaseRule phase in fases)
        {
            if (phase != null && normalizedHealth <= phase.umbralVida)
            {
                selected = phase;
            }
        }
        return selected;
    }

    private void ValidatePhaseOrder()
    {
        if (fases == null)
        {
            return;
        }
        System.Array.Sort(fases, (left, right) =>
            right.umbralVida.CompareTo(left.umbralVida));
    }

    private void OnFallaRemoved(FallaCore falla)
    {
        if (falla != null)
        {
            falla.Removida -= OnFallaRemoved;
        }
        active.Remove(falla);
    }

    private void CleanupReferences()
    {
        for (int index = active.Count - 1; index >= 0; index--)
        {
            if (active[index] == null || !active[index].gameObject.activeInHierarchy)
            {
                active.RemoveAt(index);
            }
        }
    }
}

