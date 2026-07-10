using UnityEngine;

/// <summary>
/// Maneja el combo de ataques del Cazador y la activacion de la hitbox de ataque.
/// La hitbox real (deteccion de golpe) vive en un GameObject hijo con un Collider
/// en modo Trigger, controlado por este script via animation events o timers.
/// </summary>
public class CazadorCombat : MonoBehaviour
{
    [System.Serializable]
    public class AtaqueCombo
    {
        public string nombreAnimacion;
        public float dano = 10f;
        public float duracionAnimacion = 0.5f;
        public float ventanaSiguienteComboInicio = 0.15f; // desde cuando se puede encadenar
        public float ventanaSiguienteComboFin = 0.4f;      // hasta cuando se puede encadenar
    }

    [Header("Referencias")]
    [SerializeField] private CazadorController controller;
    [SerializeField] private CazadorStats stats;
    [SerializeField] private Animator animator;
    [SerializeField] private HitboxAtaque hitboxAtaque; // ver script HitboxAtaque.cs

    [Header("Combo")]
    [SerializeField] private AtaqueCombo[] combo;
    [SerializeField] private float costoStaminaPorAtaque = 10f;

    private int indiceComboActual = -1;
    private bool estaAtacando;
    private bool inputBufferizado;
    private float timerAtaque;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<CazadorController>();
        if (stats == null) stats = GetComponent<CazadorStats>();
    }

    private void Update()
    {
        if (!stats.EstaVivo) return;

        LeerInputAtaque();

        if (estaAtacando)
        {
            ActualizarAtaqueEnCurso();
        }
    }

    private void LeerInputAtaque()
    {
        bool botonAtaquePresionado = Input.GetButtonDown("Fire1");
        if (!botonAtaquePresionado) return;

        if (!estaAtacando)
        {
            IniciarAtaque(0);
        }
        else
        {
            // Guardamos el input para encadenar el combo si estamos en la ventana correcta
            inputBufferizado = true;
        }
    }

    private void IniciarAtaque(int indice)
    {
        if (combo == null || combo.Length == 0) return;
        if (indice >= combo.Length) return;
        if (!stats.IntentarGastarStamina(costoStaminaPorAtaque)) return;

        indiceComboActual = indice;
        estaAtacando = true;
        inputBufferizado = false;
        timerAtaque = 0f;

        controller.SetPuedeActuar(false);

        AtaqueCombo ataque = combo[indiceComboActual];
        if (animator != null && !string.IsNullOrEmpty(ataque.nombreAnimacion))
        {
            animator.SetTrigger(ataque.nombreAnimacion);
        }
    }

    private void ActualizarAtaqueEnCurso()
    {
        AtaqueCombo ataqueActual = combo[indiceComboActual];
        timerAtaque += Time.deltaTime;

        bool dentroDeVentanaCombo = timerAtaque >= ataqueActual.ventanaSiguienteComboInicio
                                     && timerAtaque <= ataqueActual.ventanaSiguienteComboFin;

        if (inputBufferizado && dentroDeVentanaCombo)
        {
            int siguienteIndice = indiceComboActual + 1;
            if (siguienteIndice < combo.Length)
            {
                IniciarAtaque(siguienteIndice);
                return;
            }
        }

        if (timerAtaque >= ataqueActual.duracionAnimacion)
        {
            TerminarAtaque();
        }
    }

    private void TerminarAtaque()
    {
        estaAtacando = false;
        indiceComboActual = -1;
        inputBufferizado = false;
        controller.SetPuedeActuar(true);
    }

    /// <summary>
    /// Llamar desde un Animation Event en el frame exacto del golpe.
    /// Activa la hitbox por una ventana corta.
    /// </summary>
    public void EventoActivarHitbox()
    {
        if (indiceComboActual < 0 || hitboxAtaque == null) return;

        float dano = combo[indiceComboActual].dano;
        hitboxAtaque.Activar(dano);
    }

    /// <summary>
    /// Llamar desde un Animation Event al finalizar el frame de golpe.
    /// </summary>
    public void EventoDesactivarHitbox()
    {
        if (hitboxAtaque == null) return;
        hitboxAtaque.Desactivar();
    }
}