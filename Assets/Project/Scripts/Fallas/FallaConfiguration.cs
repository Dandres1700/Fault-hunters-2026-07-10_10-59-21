using UnityEngine;

[CreateAssetMenu(fileName = "FallaConfig", menuName = "Cazadores de Fallas/Falla Configuration")]
public sealed class FallaConfiguration : ScriptableObject
{
    [Header("Identidad")]
    [SerializeField] private FallaType tipo = FallaType.Rastrera;

    [Header("Supervivencia")]
    [SerializeField, Min(1f)] private float vidaMaxima = 45f;
    [SerializeField, Min(0f)] private float invulnerabilidadTrasImpacto = 0.08f;
    [SerializeField, Min(0f)] private float tiempoDesaparicion = 0.65f;
    [SerializeField] private bool desactivarAlMorir = true;

    [Header("Percepcion y movimiento")]
    [SerializeField, Min(0.1f)] private float rangoDeteccion = 10f;
    [SerializeField, Min(0.02f)] private float intervaloDeteccion = 0.2f;
    [SerializeField, Min(0f)] private float velocidad = 3.5f;
    [SerializeField, Min(0f)] private float velocidadRotacion = 9f;
    [SerializeField, Min(0f)] private float distanciaMinimaObjetivo = 1.15f;

    [Header("Combate")]
    [SerializeField, Min(0f)] private float dano = 12f;
    [SerializeField, Min(0.05f)] private float rangoAtaque = 1.45f;
    [SerializeField, Min(0f)] private float preparacionAtaque = 0.35f;
    [SerializeField, Min(0f)] private float cooldownAtaque = 1.15f;

    [Header("Apariencia")]
    [SerializeField] private FallaCoreVisibility visibilidadNucleo =
        FallaCoreVisibility.TrasDeteccion;
    [SerializeField, Min(0f)] private float velocidadPulsacion = 2.4f;
    [SerializeField, Range(0f, 0.5f)] private float intensidadPulsacion = 0.12f;
    [SerializeField, Range(0f, 1f)] private float intensidadDeformacion = 0.18f;

    public FallaType Tipo => tipo;
    public float VidaMaxima => vidaMaxima;
    public float InvulnerabilidadTrasImpacto => invulnerabilidadTrasImpacto;
    public float TiempoDesaparicion => tiempoDesaparicion;
    public bool DesactivarAlMorir => desactivarAlMorir;
    public float RangoDeteccion => rangoDeteccion;
    public float IntervaloDeteccion => intervaloDeteccion;
    public float Velocidad => velocidad;
    public float VelocidadRotacion => velocidadRotacion;
    public float DistanciaMinimaObjetivo => distanciaMinimaObjetivo;
    public float Dano => dano;
    public float RangoAtaque => rangoAtaque;
    public float PreparacionAtaque => preparacionAtaque;
    public float CooldownAtaque => cooldownAtaque;
    public FallaCoreVisibility VisibilidadNucleo => visibilidadNucleo;
    public float VelocidadPulsacion => velocidadPulsacion;
    public float IntensidadPulsacion => intensidadPulsacion;
    public float IntensidadDeformacion => intensidadDeformacion;
}

