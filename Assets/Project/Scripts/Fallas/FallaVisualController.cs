using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallaVisualController : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Color colorAlerta = new Color(0.7f, 0.08f, 1f, 1f);
    [SerializeField] private Color colorImpacto = new Color(1f, 0.15f, 0.2f, 1f);
    [SerializeField, Min(0.02f)] private float duracionImpacto = 0.12f;

    private MaterialPropertyBlock block;
    private FallaConfiguration configuration;
    private Vector3 initialScale;
    private Coroutine hitRoutine;
    private bool alerted;
    private bool attacking;
    private float powerMultiplier = 1f;
    private float phaseOffset;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        visualRoot ??= transform;
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
        initialScale = visualRoot.localScale;
        phaseOffset = Random.Range(0f, 10f);
    }

    public void Initialize(FallaConfiguration value)
    {
        configuration = value;
        alerted = false;
        attacking = false;
        powerMultiplier = 1f;
        if (visualRoot != null)
        {
            visualRoot.localScale = initialScale;
        }
        ApplyColor(Color.clear, false);
    }

    private void Update()
    {
        if (configuration == null || visualRoot == null)
        {
            return;
        }

        float speed = configuration.VelocidadPulsacion * (attacking ? 2.25f : 1f);
        float pulse = 1f + Mathf.Sin(Time.time * speed) * configuration.IntensidadPulsacion;
        float deform = Mathf.Sin(Time.time * speed * 0.73f + phaseOffset) *
                       configuration.IntensidadDeformacion;
        visualRoot.localScale = Vector3.Scale(
            initialScale,
            new Vector3(pulse + deform * 0.35f, pulse - deform * 0.25f, pulse + deform)
        );

        float sway = Mathf.Sin(Time.time * speed * 0.41f) *
                     configuration.IntensidadDeformacion * 7f;
        visualRoot.localRotation = Quaternion.Euler(0f, sway, sway * 0.35f);
    }

    public void SetAlerted(bool value)
    {
        alerted = value;
        if (hitRoutine == null)
        {
            ApplyCurrentColor();
        }
    }

    public void SetAttacking(bool value)
    {
        attacking = value;
        if (hitRoutine == null)
        {
            ApplyCurrentColor();
        }
    }

    public void SetPowerMultiplier(float value)
    {
        powerMultiplier = Mathf.Max(0.1f, value);
        if (hitRoutine == null)
        {
            ApplyCurrentColor();
        }
    }

    public void PlayHit()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
        }
        hitRoutine = StartCoroutine(HitRoutine());
    }

    private IEnumerator HitRoutine()
    {
        ApplyColor(colorImpacto, true);
        yield return new WaitForSeconds(duracionImpacto);
        hitRoutine = null;
        ApplyCurrentColor();
    }

    private void ApplyCurrentColor()
    {
        if (!alerted && !attacking && powerMultiplier <= 1.01f)
        {
            ApplyColor(Color.clear, false);
            return;
        }

        Color color = colorAlerta * Mathf.Clamp(powerMultiplier, 1f, 2f);
        if (attacking)
        {
            color = Color.Lerp(color, Color.white, 0.35f);
        }
        color.a = 1f;
        ApplyColor(color, true);
    }

    private void ApplyColor(Color color, bool overrideColor)
    {
        if (renderers == null || block == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }
            targetRenderer.GetPropertyBlock(block);
            if (overrideColor)
            {
                block.SetColor(BaseColorId, color);
                block.SetColor(EmissionColorId, color * 1.5f);
            }
            else
            {
                block.Clear();
            }
            targetRenderer.SetPropertyBlock(block);
        }
    }

    private void OnDisable()
    {
        if (renderers == null || block == null)
        {
            return;
        }
        block.Clear();
        foreach (Renderer targetRenderer in renderers)
        {
            targetRenderer?.SetPropertyBlock(block);
        }
    }
}
