using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class Hurtbox : MonoBehaviour, IRecibeImpacto
{
    [Tooltip("Componente central de vida que implementa IRecibeImpacto.")]
    [SerializeField] private MonoBehaviour receptor;

    private IRecibeImpacto receiver;

    public Object IdentidadImpacto => receiver?.IdentidadImpacto ?? receptor;

    protected virtual void Awake()
    {
        Collider hitCollider = GetComponent<Collider>();
        hitCollider.isTrigger = true;
        ResolveReceiver();
    }

    public void Configurar(MonoBehaviour value)
    {
        receptor = value;
        ResolveReceiver();
    }

    public bool RecibirImpacto(DamageInfo impacto)
    {
        return receiver != null && receiver.RecibirImpacto(impacto);
    }

    public void RecibirDano(float cantidad)
    {
        Vector3 point = GetComponent<Collider>().ClosestPoint(transform.position);
        RecibirImpacto(new DamageInfo(cantidad, point, Vector3.zero, null));
    }

    private void ResolveReceiver()
    {
        receptor ??= GetComponentsInParent<MonoBehaviour>(true)
            .FirstOrDefault(component => component is IRecibeImpacto);
        receiver = receptor as IRecibeImpacto;
        if (receiver == null)
        {
            Debug.LogError("Hurtbox necesita un receptor IRecibeImpacto central.", this);
        }
    }
}
