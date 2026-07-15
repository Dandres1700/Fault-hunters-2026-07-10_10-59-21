using UnityEngine;

/// <summary>
/// Centraliza la seleccion de objetivos hostiles sin depender del nombre del GameObject.
/// La capa 8 no es suficiente porque el prefab historico del Mutant tambien la utiliza.
/// </summary>
public static class CombatTargeting
{
    public static bool TryGetCazador(Collider candidate, out IRecibeImpacto receiver)
    {
        receiver = candidate != null
            ? candidate.GetComponentInParent<IRecibeImpacto>()
            : null;
        return IsCazador(receiver);
    }

    public static bool IsCazador(IRecibeImpacto receiver)
    {
        return receiver != null &&
               (receiver is CazadorStats || receiver.IdentidadImpacto is CazadorStats);
    }

    public static CazadorStats GetCazadorStats(IRecibeImpacto receiver)
    {
        if (receiver is CazadorStats direct)
        {
            return direct;
        }
        return receiver?.IdentidadImpacto as CazadorStats;
    }
}

