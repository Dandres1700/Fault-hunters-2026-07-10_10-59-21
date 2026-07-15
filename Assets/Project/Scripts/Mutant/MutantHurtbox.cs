using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MutantHurtbox : MonoBehaviour, IRecibeDano
{
    [SerializeField] private MutantStats receptor;

    private void Awake()
    {
        receptor ??= GetComponentInParent<MutantStats>();
        Collider hurtboxCollider = GetComponent<Collider>();
        hurtboxCollider.isTrigger = true;

        if (receptor == null)
        {
            Debug.LogError("MutantHurtbox no encontro MutantStats en sus padres.", this);
        }
    }

    public void RecibirDano(float cantidad)
    {
        receptor?.RecibirDano(cantidad);
    }
}
