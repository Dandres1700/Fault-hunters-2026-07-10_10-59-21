using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collider (Trigger) que representa el "arma" del Cazador durante un ataque.
/// Se activa/desactiva desde CazadorCombat en frames especificos de la animacion.
/// Colocar en un GameObject hijo (ej. en la mano o el arma) con un Collider
/// marcado como "Is Trigger".
/// </summary>
[RequireComponent(typeof(Collider))]
public class HitboxAtaque : MonoBehaviour
{
    [SerializeField] private LayerMask capasGolpeables; // ej. layer "Boss" / "Enemigo"

    private Collider hitCollider;
    private float danoActual;
    private HashSet<Collider> objetivosYaGolpeados = new HashSet<Collider>();

    private void Awake()
    {
        hitCollider = GetComponent<Collider>();
        hitCollider.isTrigger = true;
        hitCollider.enabled = false;
    }

    public void Activar(float dano)
    {
        danoActual = dano;
        objetivosYaGolpeados.Clear();
        hitCollider.enabled = true;
    }

    public void Desactivar()
    {
        hitCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & capasGolpeables) == 0) return;
        if (objetivosYaGolpeados.Contains(other)) return;

        objetivosYaGolpeados.Add(other);

        // El boss debe exponer un metodo de recibir dano, por ejemplo:
        // FallaBoss falla = other.GetComponent<FallaBoss>();
        // falla?.RecibirDano(danoActual);
        var recibidorDano = other.GetComponent<IRecibeDano>();
        recibidorDano?.RecibirDano(danoActual);
    }
}

/// <summary>
/// Interfaz comun para cualquier cosa que pueda recibir dano
/// (bosses, obstaculos destructibles, etc). FallaBoss la va a implementar.
/// </summary>
public interface IRecibeDano
{
    void RecibirDano(float cantidad);
}