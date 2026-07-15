using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FallaCore))]
public sealed class FallaGenerator : MonoBehaviour
{
    [Header("Invocacion")]
    [SerializeField] private GameObject[] prefabsPermitidos;
    [SerializeField] private Transform[] puntosAparicion;
    [SerializeField, Min(1)] private int maximoActivas = 3;
    [SerializeField, Min(0.1f)] private float intervaloGeneracion = 5f;
    [SerializeField, Min(0f)] private float radioAparicion = 2.5f;
    [SerializeField, Min(0.1f)] private float radioLibre = 0.65f;
    [SerializeField, Min(0f)] private float distanciaMinimaObjetivo = 2.5f;
    [SerializeField, Range(1, 20)] private int intentosPosicion = 8;
    [SerializeField] private LayerMask capasSuelo = 1 << 9;
    [SerializeField] private LayerMask capasBloqueo = ~0;
    [SerializeField] private ParticleSystem particulasGeneracion;

    private readonly List<FallaCore> active = new List<FallaCore>();
    private FallaCore core;
    private float timer;
    private bool spawningEnabled = true;

    public int CantidadActiva => active.Count;
    public event System.Action<FallaCore> Generada;

    private void Awake()
    {
        core = GetComponent<FallaCore>();
    }

    private void OnEnable()
    {
        timer = intervaloGeneracion;
        spawningEnabled = true;
        if (core != null)
        {
            core.Murio += OnOwnerDied;
        }
    }

    private void OnDisable()
    {
        if (core != null)
        {
            core.Murio -= OnOwnerDied;
        }
        spawningEnabled = false;
    }

    private void Update()
    {
        if (!spawningEnabled || core == null || !core.EstaViva || active.Count >= maximoActivas)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }
        timer = intervaloGeneracion;
        TrySpawn();
    }

    public bool TrySpawn()
    {
        CleanupReferences();
        if (!spawningEnabled || active.Count >= maximoActivas ||
            prefabsPermitidos == null || prefabsPermitidos.Length == 0)
        {
            return false;
        }

        GameObject prefab = prefabsPermitidos[Random.Range(0, prefabsPermitidos.Length)];
        if (prefab == null || prefab.GetComponent<FallaCore>() == null)
        {
            Debug.LogWarning("FallaGenerator contiene un prefab invalido.", this);
            return false;
        }

        Transform spawnPoint = puntosAparicion != null && puntosAparicion.Length > 0
            ? puntosAparicion[Random.Range(0, puntosAparicion.Length)]
            : transform;
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        if (!FallaSpawnUtility.TryFindValidPosition(
                spawnPoint.position,
                radioAparicion,
                radioLibre,
                distanciaMinimaObjetivo,
                core.Objetivo,
                capasSuelo,
                capasBloqueo,
                intentosPosicion,
                out Vector3 position))
        {
            return false;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        FallaCore spawned = instance.GetComponent<FallaCore>();
        spawned.SetTarget(core.Objetivo);
        spawned.Removida += OnSpawnRemoved;
        active.Add(spawned);
        particulasGeneracion?.Play(true);
        Generada?.Invoke(spawned);
        return true;
    }

    public void StopSpawning() => spawningEnabled = false;

    private void OnSpawnRemoved(FallaCore removed)
    {
        if (removed != null)
        {
            removed.Removida -= OnSpawnRemoved;
        }
        active.Remove(removed);
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

    private void OnOwnerDied(FallaCore owner)
    {
        spawningEnabled = false;
    }
}
