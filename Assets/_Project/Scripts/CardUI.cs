using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    public Image artwork;
    public TMPro.TextMeshProUGUI nameText;
    private UnitDataSO data;

    public void Init(UnitDataSO unit)
    {
        data = unit;

        /*
        if (unit.cardArtwork != null)
        {
            artwork.sprite = unit.cardArtwork;
            artwork.type = Image.Type.Simple;
            artwork.preserveAspect = true;

            var aspect = artwork.sprite.rect.width / artwork.sprite.rect.height;
            float targetHeight = GetComponent<RectTransform>().rect.height;
            float targetWidth = targetHeight * aspect;
            artwork.rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
        }

        if (nameText != null)
        {
            nameText.text = unit.unitName;
        }
        */
    }

    [System.Obsolete]
    public void OnClick()
    {
        if (FindObjectOfType<BenchManager>().TryAddToBench(data))
        {
            Destroy(gameObject); // Remove card from hand
        }
    }
}
