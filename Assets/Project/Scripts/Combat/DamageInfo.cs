using UnityEngine;

public readonly struct DamageInfo
{
    public DamageInfo(
        float cantidad,
        Vector3 puntoImpacto,
        Vector3 direccionImpacto,
        GameObject fuente
    )
    {
        Cantidad = Mathf.Max(0f, cantidad);
        PuntoImpacto = puntoImpacto;
        DireccionImpacto = direccionImpacto.sqrMagnitude > 0.0001f
            ? direccionImpacto.normalized
            : Vector3.zero;
        Fuente = fuente;
    }

    public float Cantidad { get; }
    public Vector3 PuntoImpacto { get; }
    public Vector3 DireccionImpacto { get; }
    public GameObject Fuente { get; }
}

/// <summary>
/// Extension compatible de IRecibeDano que conserva punto, direccion y origen.
/// IdentidadImpacto permite que varias hurtboxes compartan una sola vida.
/// </summary>
public interface IRecibeImpacto : IRecibeDano
{
    Object IdentidadImpacto { get; }
    bool RecibirImpacto(DamageInfo impacto);
}
