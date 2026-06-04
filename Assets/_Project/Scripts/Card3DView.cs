using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Card3DView : MonoBehaviour
{
    [Header("Mesh References")]
    [SerializeField] private GameObject topFaceObject;
    [SerializeField] private GameObject bottomFaceObject;

    [Header("Text Elements")]
    [SerializeField] private TextMeshPro nameText;
    [SerializeField] private TextMeshPro specializationText;
    [SerializeField] private TextMeshPro costText;

    [Header("Class Related")]
    [SerializeField] private ClassIconLibrary classIconLibrary;
    [SerializeField] private Image classIconImage;

    [Header("Flag Related")]
    [SerializeField] private OriginFlagLibrary originFlagLibrary;

    private UnitDataSO unit;
    private MeshRenderer frontRenderer;
    private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    private TextMeshPro[] cachedTextMeshes = System.Array.Empty<TextMeshPro>();
    private readonly Dictionary<Renderer, Color?> rendererBaseColors = new();
    private readonly Dictionary<TextMeshPro, Color> textBaseColors = new();
    private Color classIconBaseColor = Color.white;
    private bool visualCacheInitialized;

    void Awake()
    {
        frontRenderer = topFaceObject.GetComponent<MeshRenderer>();
        CacheVisualTargets();
    }

    private void applyFlag(Texture2D flagTexture)
    {
        Vector4 uvRect = new Vector4(0.21f, 0.18f, 0.08f, 0.05375f);

        var mpb = new MaterialPropertyBlock();
        frontRenderer.GetPropertyBlock(mpb);

        mpb.SetTexture("_FlagTex", flagTexture);
        mpb.SetVector("_FlagUVRect", uvRect);

        frontRenderer.SetPropertyBlock(mpb);
    }

    public void Init(UnitDataSO data)
    {
        unit = data;

        // FRONT ART
        if (unit.cardArtwork != null)
        {
            frontRenderer.material.SetTexture("_MainTex", unit.cardArtwork);
            // frontRenderer.material.SetTexture("_NoiseTexture", unit.cardArtwork);
            // frontRenderer.material.SetTexture("_MainTex", unit.cardArtwork);
            Debug.Log($"Init card: unit={unit}, unit.Data={unit.Data}, originFlagLibrary={originFlagLibrary}");
            Texture2D flag = originFlagLibrary.GetFlag(unit.Data.Origin);
            if (flag != null)
            {
                applyFlag(flag);
            }
            else
            {
                Debug.LogWarning($"Could not find a country flag for {unit.Data.Origin}.");
            }
        }

        // NAME
        if (nameText != null)
        {
            nameText.text = unit.Data.UnitName;
        }
        if (specializationText != null)
        {
            specializationText.text = unit.Data.Role.ToString();
        }
        if (costText != null)
        {
            costText.text = unit.Data.Cost.ToString();
        }
        if (classIconImage != null)
        {
            var icon = classIconLibrary.GetIcon(data.Data.Class);
            classIconImage.sprite = icon;
        }

        // BACK ART (optional)
        // MeshRenderer backRenderer = bottomFaceObject.GetComponent<MeshRenderer>();
        // backRenderer.material.SetTexture("_MainTex", unit.cardBackTexture);
    }

    public void SetVisualAlpha(float alpha)
    {
        CacheVisualTargets();
        float clampedAlpha = Mathf.Clamp01(alpha);

        foreach (KeyValuePair<Renderer, Color?> pair in rendererBaseColors)
        {
            if (pair.Key == null || !pair.Value.HasValue)
            {
                continue;
            }

            Color tintedColor = pair.Value.Value;
            tintedColor.a *= clampedAlpha;

            MaterialPropertyBlock propertyBlock = new();
            pair.Key.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", tintedColor);
            propertyBlock.SetColor("_BaseColor", tintedColor);
            pair.Key.SetPropertyBlock(propertyBlock);
        }

        foreach (KeyValuePair<TextMeshPro, Color> pair in textBaseColors)
        {
            if (pair.Key == null)
            {
                continue;
            }

            Color tintedColor = pair.Value;
            tintedColor.a *= clampedAlpha;
            pair.Key.color = tintedColor;
        }

        if (classIconImage != null)
        {
            Color tintedColor = classIconBaseColor;
            tintedColor.a *= clampedAlpha;
            classIconImage.color = tintedColor;
        }
    }

    private void CacheVisualTargets()
    {
        if (visualCacheInitialized)
        {
            return;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedTextMeshes = GetComponentsInChildren<TextMeshPro>(true);

        rendererBaseColors.Clear();
        foreach (Renderer rendererTarget in cachedRenderers)
        {
            if (rendererTarget == null)
            {
                continue;
            }

            if (rendererTarget.sharedMaterial == null)
            {
                rendererBaseColors[rendererTarget] = null;
                continue;
            }

            Material material = rendererTarget.sharedMaterial;
            if (material.HasProperty("_Color"))
            {
                rendererBaseColors[rendererTarget] = material.color;
            }
            else if (material.HasProperty("_BaseColor"))
            {
                rendererBaseColors[rendererTarget] = material.GetColor("_BaseColor");
            }
            else
            {
                rendererBaseColors[rendererTarget] = null;
            }
        }

        textBaseColors.Clear();
        foreach (TextMeshPro textMesh in cachedTextMeshes)
        {
            if (textMesh != null)
            {
                textBaseColors[textMesh] = textMesh.color;
            }
        }

        if (classIconImage != null)
        {
            classIconBaseColor = classIconImage.color;
        }

        visualCacheInitialized = true;
    }
}
