using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using System.Linq;
using UnityEngine.InputSystem.Utilities;
using AIRBattleSimulation;

namespace CardSystem
{
    public class HandManager : MonoBehaviour
    {
        private Camera mainCamera;

        [Header("Prefabs & References")]
        [SerializeField] private Camera cardCameraPrefab;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform handParent; // 3D empty parent in world space
        [SerializeField] private UnitPoolManager poolManager;

        [Header("Hand Settings")]
        [SerializeField] private int handSize = 5;

        public List<UnitDataSO> allUnits;

        //private List<UnitDataSO> currentHand = new();
        //private List<GameObject> currentCardObjects = new();

        private List<Card> hand = new();
        public List<Card> Hand
        {
            get => hand;
            set
            {
                hand = value;
            }
        }
        public IReadOnlyList<GameObject> HandGameObjects =>
            hand.Select(c => c.CardObject).ToArray();
        public IReadOnlyList<UnitDataSO> HandUnitData =>
            hand.Select(c => c.Unit).ToArray();
        public readonly HashSet<GameObject> draggedCards = new();

        private CardHandDisplayer cardHandDisplayer;

        private void Awake()
        {
            mainCamera = Camera.main;
            cardHandDisplayer = GetComponent<CardHandDisplayer>();
            if (cardHandDisplayer != null)
            {
                cardHandDisplayer.SetLayoutHandSize(handSize);
            }
        }

        void Start()
        {
            if (poolManager == null || poolManager.unitRegistry == null)
            {
                Debug.LogError("HandManager: Missing UnitPoolManager or UnitRegistry.");
                return;
            }

            allUnits = new List<UnitDataSO>(poolManager.unitRegistry.units);

            if (allUnits.Count == 0)
            {
                Debug.LogError("HandManager: No units found in registry.");
                return;
            }

            InstantiateCardCameras();

            GenerateHand();
        }

        public void OnCardDragChanged(GameObject card, bool isDragging)
        {
            if (isDragging)
                draggedCards.Add(card);
            else
                draggedCards.Remove(card);
        }

        private void InstantiateCardCameras()
        {
            UniversalAdditionalCameraData urpMain = mainCamera.GetUniversalAdditionalCameraData();

            // Generate Card Cameras based on the total hand size, for proper render stacks
            for (int i = 0; i < handSize; i++)
            {
                Camera newCardCam = Instantiate(cardCameraPrefab);
                newCardCam.name = $"CardCamera{i}";
                newCardCam.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
                newCardCam.transform.SetParent(mainCamera.transform);

                int layerDesignatedForCamera = LayerMask.NameToLayer($"Card{i}");
                newCardCam.cullingMask = 1 << layerDesignatedForCamera;
                newCardCam.depth = mainCamera.depth + i + 1;

                urpMain.cameraStack.Add(newCardCam);
                UniversalAdditionalCameraData urpCardCam = newCardCam.GetUniversalAdditionalCameraData();
                urpCardCam.renderType = CameraRenderType.Overlay;
            }

            // Generate one extra card camera for 'focused' cards (hovered/dragged)
            Camera focusCam = Instantiate(cardCameraPrefab);
            focusCam.name = "CardCameraFocused";
            focusCam.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
            focusCam.transform.SetParent(mainCamera.transform);

            int focusLayer = LayerMask.NameToLayer("CardFocused");
            focusCam.cullingMask = 1 << focusLayer;
            focusCam.depth = mainCamera.depth + handSize + 1 + 100;

            urpMain.cameraStack.Add(focusCam);
            UniversalAdditionalCameraData urpFocusCam = focusCam.GetUniversalAdditionalCameraData();
            urpFocusCam.renderType = CameraRenderType.Overlay;
        }

        public List<Card> GetHand()
        {
            return hand;
        }

        public bool IsCardBeingDragged(GameObject exceptCard = null)
        {
            for (int i = 0; i < HandGameObjects.Count; i++)
            {
                if (exceptCard != null && exceptCard == HandGameObjects[i])
                {
                    continue;
                }
                var state = HandGameObjects[i].GetComponent<CardState>();
                if (state && state.IsDragging)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsHandLowered()
        {
            return cardHandDisplayer != null && cardHandDisplayer.IsHandLowered;
        }

        public void GenerateHand()
        {
            ClearHand();

            for (int i = 0; i < handSize; i++)
            {
                var unit = allUnits[Random.Range(0, allUnits.Count)];
                // GameObject card = CreateCard(unit, i);
                Card card = CreateCard(unit, i);
                hand.Add(card);
            }

            cardHandDisplayer.HandleRecentGenerationStandup();
        }

        private Card CreateCard(UnitDataSO unit, int handSlot)
        {
            // Spawn slightly below the camera so cards fold upward nicely
            Vector3 spawnPosition = mainCamera.transform.position
                            + mainCamera.transform.forward * cardHandDisplayer.distanceFromCamera
                            - mainCamera.transform.up * Mathf.Abs(cardHandDisplayer.spawnVerticalOffsetFromCamera);
            var newCardLayer = LayerMask.NameToLayer($"Card{handSlot}");
            var newCard = new Card(unit, cardPrefab, gameObject.transform, spawnPosition, newCardLayer, this);
            newCard.CardObject.transform.SetParent(gameObject.transform, worldPositionStays: true);
            return newCard;
        }

        /*
        private GameObject CreateCard(UnitDataSO unit, int handSlot)
        {
            // Spawn slightly below the camera so cards fold upward nicely
            Vector3 spawnPosition = mainCamera.transform.position
                            + mainCamera.transform.forward * cardHandDisplayer.distanceFromCamera
                            - mainCamera.transform.up * Mathf.Abs(cardHandDisplayer.spawnVerticalOffsetFromCamera);

            GameObject card = Instantiate(cardPrefab, spawnPosition, Quaternion.identity);
            card.GetComponent<CardMotionController>().SetHandManager(this);
            card.layer = LayerMask.NameToLayer($"Card{handSlot}");

            LayerUtils.SetLayerRecursive(card, card.layer);

            Card3DView cardView = card.GetComponent<Card3DView>();
            if (cardView != null)
            {
                cardView.Init(unit);
            }
            else
            {
                Debug.LogError("HandManager: Spawned card prefab is missing Card3DView component.");
            }
            return card;
        }
        */

        void ClearHand()
        {
            foreach (Card card in hand)
            {
                card.Destroy();
            }
            hand.Clear();
        }

        public void Reroll()
        {
            if (GameManager.Instance.gold >= 2)
            {
                GameManager.Instance.gold -= 2;
                GenerateHand();
                GameManager.Instance.UpdateUI();
            }
        }

        public void OnCardHoverChanged(Card card, bool hovered)
        {

        }

        public void OnCardDragChanged(Card card, bool dragged)
        {

        }

        public void PlayCard(GameObject cardObj)
        {
            int index = HandGameObjects.ToList().IndexOf(cardObj);
            if (index < 0)
            {
                return;
            }
            Card card = hand[index];

            UnitDataSO data = HandUnitData[index];
            BenchManager bench = FindFirstObjectByType<BenchManager>();

            if (bench == null)
            {
                Debug.LogError("BenchManager not found");
            }

            if (!bench.CanAdd(data))
            {
                Debug.Log("Cannot play card: Bench is full or merge conditions unmet");
                return;
            }

            if (GameManager.Instance.gold < data.cost)
            {
                Debug.Log("Not enough gold to play card");
                return;
            }

            GameManager.Instance.gold -= data.cost;
            GameManager.Instance.UpdateUI();

            if (!bench.TryAddToBench(data))
            {
                Debug.LogWarning("Failed to add unit to bench despite pre-check");
                GameManager.Instance.gold += data.cost; // revert
                GameManager.Instance.UpdateUI();
                return;
            }

            card.Destroy();
            hand.RemoveAt(index);
        }
    }
}