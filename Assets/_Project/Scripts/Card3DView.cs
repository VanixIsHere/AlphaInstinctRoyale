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

    private UnitDataSO unit;
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
}
