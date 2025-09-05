using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private UnitDataSO data;
    private MeshRenderer frontRenderer;

    void Awake()
    {
        frontRenderer = topFaceObject.GetComponent<MeshRenderer>();
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

    public void Init(UnitDataSO unit)
    {
        data = unit;

        // FRONT ART
        if (unit.cardArtwork != null)
        {
            frontRenderer.material.SetTexture("_MainTex", unit.cardArtwork);
            // frontRenderer.material.SetTexture("_NoiseTexture", unit.cardArtwork);
            // frontRenderer.material.SetTexture("_MainTex", unit.cardArtwork);
            Texture2D flag = originFlagLibrary.GetFlag(unit.origin);
            if (flag != null)
            {
                applyFlag(flag);
            }
            else
            {
                Debug.LogWarning($"Could not find a country flag for {unit.origin}.");
            }
            Debug.Log("Shader name: " + frontRenderer.material.shader.name);
        }

        // NAME
        if (nameText != null)
        {
            nameText.text = unit.unitName;
        }
        if (specializationText != null)
        {
            specializationText.text = unit.role.ToString();
        }
        if (costText != null)
        {
            costText.text = unit.cost.ToString();
        }
        if (classIconImage != null)
        {
            var icon = classIconLibrary.GetIcon(data.UnitClass);
            classIconImage.sprite = icon;
        }

        // BACK ART (optional)
        // MeshRenderer backRenderer = bottomFaceObject.GetComponent<MeshRenderer>();
        // backRenderer.material.SetTexture("_MainTex", unit.cardBackTexture);
    }
}
