using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageEffects : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int HitHash = Animator.StringToHash("Hit");

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer[] renderers;

    [Header("Flash")]
    [SerializeField] private Color colorImpacto = new Color(1f, 0.2f, 0.12f, 1f);
    [SerializeField, Min(0.01f)] private float duracionFlash = 0.12f;

    [Header("Recursos opcionales")]
    [SerializeField] private ParticleSystem particulasImpacto;
    [SerializeField] private AudioClip sonidoImpacto;
    [SerializeField, Range(0f, 1f)] private float volumenImpacto = 1f;

    private readonly List<MaterialPropertyBlock> originalBlocks =
        new List<MaterialPropertyBlock>();
    private Coroutine flashRoutine;
    private bool hasHitParameter;

    private void Awake()
    {
        animator ??= GetComponentInChildren<Animator>(true);
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        CacheOriginalBlocks();
        hasHitParameter = animator != null &&
                          animator.parameters.Any(parameter => parameter.nameHash == HitHash);
    }

    public void Reproducir(DamageInfo impacto)
    {
        if (hasHitParameter)
        {
            animator.SetTrigger(HitHash);
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            RestoreBlocks();
        }

        flashRoutine = StartCoroutine(FlashRoutine());
        SpawnOptionalEffects(impacto);
    }

    private IEnumerator FlashRoutine()
    {
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, colorImpacto);
            block.SetColor(ColorId, colorImpacto);
            renderer.SetPropertyBlock(block);
        }

        yield return new WaitForSeconds(duracionFlash);
        RestoreBlocks();
        flashRoutine = null;
    }

    private void CacheOriginalBlocks()
    {
        originalBlocks.Clear();
        foreach (Renderer renderer in renderers)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            if (renderer != null)
            {
                renderer.GetPropertyBlock(block);
            }

            originalBlocks.Add(block);
        }
    }

    private void RestoreBlocks()
    {
        for (int index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null && index < originalBlocks.Count)
            {
                renderers[index].SetPropertyBlock(originalBlocks[index]);
            }
        }
    }

    private void SpawnOptionalEffects(DamageInfo impacto)
    {
        if (particulasImpacto != null)
        {
            Quaternion rotation = impacto.DireccionImpacto.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(-impacto.DireccionImpacto)
                : Quaternion.identity;
            ParticleSystem effect = Instantiate(
                particulasImpacto,
                impacto.PuntoImpacto,
                rotation
            );
            ParticleSystem.MainModule main = effect.main;
            Destroy(effect.gameObject, main.duration + main.startLifetime.constantMax + 0.5f);
        }

        if (sonidoImpacto != null)
        {
            AudioSource.PlayClipAtPoint(
                sonidoImpacto,
                impacto.PuntoImpacto,
                volumenImpacto
            );
        }
    }

    private void OnDisable()
    {
        RestoreBlocks();
    }
}
