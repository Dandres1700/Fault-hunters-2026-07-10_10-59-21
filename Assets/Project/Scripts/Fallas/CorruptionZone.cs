using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CorruptionZone : MonoBehaviour
{
    [SerializeField, Min(0f)] private float danoPorPulso = 6f;
    [SerializeField, Min(0.1f)] private float intervaloDano = 1f;
    [SerializeField, Min(0.1f)] private float duracion = 6f;
    [SerializeField] private LayerMask capasAfectadas = 1 << 8;
    [SerializeField] private Transform visualRoot;

    private readonly Dictionary<UnityEngine.Object, float> nextDamageTime =
        new Dictionary<UnityEngine.Object, float>();
    private float elapsed;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        visualRoot ??= transform;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float normalized = Mathf.Clamp01(elapsed / duracion);
        if (visualRoot != null)
        {
            float scale = Mathf.Sin(normalized * Mathf.PI);
            visualRoot.localScale = new Vector3(scale, 1f, scale);
        }
        if (elapsed >= duracion)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if ((capasAfectadas.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }
        IRecibeImpacto receiver = other.GetComponentInParent<IRecibeImpacto>();
        UnityEngine.Object identity = receiver?.IdentidadImpacto;
        if (receiver == null || identity == null ||
            (nextDamageTime.TryGetValue(identity, out float next) && Time.time < next))
        {
            return;
        }
        nextDamageTime[identity] = Time.time + intervaloDano;
        receiver.RecibirImpacto(new DamageInfo(
            danoPorPulso, other.ClosestPoint(transform.position), Vector3.up, gameObject));
    }

    private void OnTriggerExit(Collider other)
    {
        IRecibeImpacto receiver = other.GetComponentInParent<IRecibeImpacto>();
        if (receiver?.IdentidadImpacto != null)
        {
            nextDamageTime.Remove(receiver.IdentidadImpacto);
        }
    }
}
