using UnityEngine;

namespace CardSystem
{
    public class Card
    {
        // State
        public bool IsHovered { get; private set; }
        public bool IsDragged { get; private set; }

        // Reference to the visual GameObject
        public GameObject CardObject { get; private set; }

        // Optional: Backref to HandManager, or use events
        private HandManager handManager;

        // Card data (could be a ScriptableObject or just properties)
        public UnitDataSO Unit { get; private set; }

        private string focusedLayerName = "CardFocused";

        public Card(UnitDataSO data, GameObject prefab, Transform parent, Vector3 spawnPosition, int layerMask, HandManager manager)
        {
            Unit = data;
            handManager = manager;

            var cardObject = Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
            cardObject.layer = layerMask;
            LayerUtils.SetLayerRecursive(cardObject, layerMask);

            // NEED TO SET UP CARD MOTION CONTROLLER
            cardObject.GetComponent<CardMotionController>().SetHandManager(manager);

            // NEED TO SET UP CARD 3D VIEW
            cardObject.GetComponent<Card3DView>().Init(data);

            // var view = CardObject.GetComponent<Card3DView>();
            // view.SetCard(this);

            CardObject = cardObject;
        }

        public void SetHovered(bool hovered)
        {
            if (IsHovered != hovered)
            {
                IsHovered = hovered;
                // You can notify HandManager, or fire an event
                handManager.OnCardHoverChanged(this, hovered);
            }
        }

        public void SetDragged(bool dragged)
        {
            if (IsDragged != dragged)
            {
                IsDragged = dragged;
                handManager.OnCardDragChanged(this, dragged);
            }
        }

        public bool CanBePlayed()
        {
            return true;
        }

        public void Play()
        {
            
        }

        public void Destroy()
        {
            if (CardObject != null)
            {
                GameObject.Destroy(CardObject);
                CardObject = null;
            }
            // Remove from HandManager, etc. if needed
        }
    }
}
