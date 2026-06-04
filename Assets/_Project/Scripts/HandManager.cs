using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using System.Linq;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using TMPro;
using AIRBattleSimulation;
using AIR.Shared.GameSession;

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
        private Button rerollButton;
        private Button lockButton;
        private TMP_Text lockButtonText;
        private readonly List<string> lastRenderedOfferIds = new();
        private Phase lastObservedPhase = Phase.GameStart;
        private bool cardsInteractable;

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
            BindShopButtons();
            SubscribeToAuthority();
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

        public bool AreCardsInteractable()
        {
            return cardsInteractable;
        }

        public int GetLayoutHandSize()
        {
            return handSize;
        }

        public void GenerateHand()
        {
            ClearHand();

            for (int i = 0; i < handSize; i++)
            {
                var unit = allUnits[UnityEngine.Random.Range(0, allUnits.Count)];
                Card card = CreateCard(unit, Guid.NewGuid().ToString("N"), i);
                hand.Add(card);
            }

            cardHandDisplayer.HandleRecentGenerationStandup();
        }

        private Card CreateCard(UnitDataSO unit, string offerId, int handSlot)
        {
            // Spawn slightly below the camera so cards fold upward nicely
            Vector3 spawnPosition = mainCamera.transform.position
                            + mainCamera.transform.forward * cardHandDisplayer.distanceFromCamera
                            - mainCamera.transform.up * Mathf.Abs(cardHandDisplayer.spawnVerticalOffsetFromCamera);
            var newCardLayer = LayerMask.NameToLayer($"Card{handSlot}");
            var newCard = new Card(unit, offerId, cardPrefab, gameObject.transform, spawnPosition, newCardLayer, this);
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
            lastRenderedOfferIds.Clear();
        }

        public void Reroll()
        {
            if (GameManager.Instance?.AuthorityClient == null)
            {
                return;
            }

            CommandResult result = GameManager.Instance.AuthorityClient.SendCommand(new COM_RerollShop
            {
                PlayerId = GameManager.LocalPlayerId,
                CorrelationId = Guid.NewGuid().ToString("N"),
            });

            if (!result.Accepted)
            {
                Debug.Log($"Reroll rejected: {result.RejectionReason} - {result.RejectionMessage}");
            }
        }

        public void ToggleLock()
        {
            if (GameManager.Instance?.AuthorityClient == null)
            {
                return;
            }

            bool isLocked = GameManager.Instance.GetLocalPlayerSnapshot()?.Shop?.IsLocked ?? false;
            MatchCommand command = isLocked
                ? new COM_UnlockShopHand()
                : new COM_LockShopHand();

            command.PlayerId = GameManager.LocalPlayerId;
            command.CorrelationId = Guid.NewGuid().ToString("N");

            CommandResult result = GameManager.Instance.AuthorityClient.SendCommand(command);
            if (!result.Accepted)
            {
                Debug.Log($"Hand lock toggle rejected: {result.RejectionReason} - {result.RejectionMessage}");
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
            if (GameManager.Instance?.AuthorityClient == null)
            {
                return;
            }

            CommandResult result = GameManager.Instance.AuthorityClient.SendCommand(new COM_BuyShopUnit
            {
                PlayerId = GameManager.LocalPlayerId,
                CorrelationId = Guid.NewGuid().ToString("N"),
                OfferId = card.OfferId,
            });

            if (!result.Accepted)
            {
                Debug.Log($"Buy rejected: {result.RejectionReason} - {result.RejectionMessage}");
            }
        }

        private void SubscribeToAuthority()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            GameManager.Instance.SnapshotUpdated -= HandleSnapshotUpdated;
            GameManager.Instance.SnapshotUpdated += HandleSnapshotUpdated;

            if (GameManager.Instance.CurrentSnapshot != null)
            {
                HandleSnapshotUpdated(GameManager.Instance.CurrentSnapshot);
            }
        }

        private void BindShopButtons()
        {
            rerollButton = GameObject.Find("RerollButton")?.GetComponent<Button>();
            lockButton = GameObject.Find("LockButton")?.GetComponent<Button>();
            lockButtonText = lockButton?.GetComponentInChildren<TMP_Text>(true);

            if (lockButton != null)
            {
                lockButton.onClick.RemoveListener(ToggleLock);
                lockButton.onClick.AddListener(ToggleLock);
            }
        }

        private void HandleSnapshotUpdated(MatchSnapshot snapshot)
        {
            Phase phase = snapshot?.Phase ?? Phase.GameStart;
            PlayerSnapshot playerSnapshot = snapshot?.GetPlayer(GameManager.LocalPlayerId);
            if (playerSnapshot == null || playerSnapshot.Shop == null)
            {
                ClearHand();
                ApplyPhaseVisualState(phase, false);
                UpdateShopButtons(null, phase);
                lastObservedPhase = phase;
                return;
            }

            bool enteredSetupPhase = lastObservedPhase != Phase.Setup && phase == Phase.Setup;
            bool shopChanged = HaveShopOffersChanged(playerSnapshot.Shop);

            if (shopChanged)
            {
                RenderShop(playerSnapshot.Shop, phase == Phase.Setup);
            }
            else if (enteredSetupPhase)
            {
                cardHandDisplayer?.HandleRecentGenerationStandup();
            }

            ApplyPhaseVisualState(phase, enteredSetupPhase && !shopChanged);
            UpdateShopButtons(playerSnapshot, phase);
            lastObservedPhase = phase;
        }

        private void UpdateShopButtons(PlayerSnapshot playerSnapshot, Phase phase)
        {
            bool isSetupPhase = phase == Phase.Setup;
            bool isLocked = playerSnapshot?.Shop?.IsLocked ?? false;

            if (rerollButton != null)
            {
                rerollButton.interactable = isSetupPhase && !isLocked;
            }

            if (lockButton != null)
            {
                lockButton.interactable = isSetupPhase;
            }

            if (lockButtonText != null)
            {
                lockButtonText.text = isLocked ? "Unlock" : "Lock";
            }
        }

        private bool HaveShopOffersChanged(ShopState shopState)
        {
            if (shopState == null || shopState.Offers.Count != lastRenderedOfferIds.Count)
            {
                return true;
            }

            for (int index = 0; index < shopState.Offers.Count; index++)
            {
                if (shopState.Offers[index].OfferId != lastRenderedOfferIds[index])
                {
                    return true;
                }
            }

            return hand.Count != shopState.Offers.Count;
        }

        private void ApplyPhaseVisualState(Phase phase, bool enteredSetupPhase)
        {
            bool isSetupPhase = phase == Phase.Setup;
            cardsInteractable = isSetupPhase;

            if (!isSetupPhase)
            {
                draggedCards.Clear();
            }

            foreach (Card card in hand)
            {
                if (card?.CardObject == null)
                {
                    continue;
                }

                CardState cardState = card.CardObject.GetComponent<CardState>();
                if (cardState != null && !isSetupPhase)
                {
                    cardState.IsDragging = false;
                    cardState.IsHovering = false;
                }

                Card3DView view = card.CardObject.GetComponent<Card3DView>();
                if (view != null)
                {
                    view.SetVisualAlpha(isSetupPhase ? 1f : 0.5f);
                }
            }

            if (isSetupPhase && enteredSetupPhase)
            {
                cardHandDisplayer?.HandleRecentGenerationStandup();
            }
        }

        private void RenderShop(ShopState shopState, bool shouldStandUp)
        {
            ClearHand();

            int renderCount = Mathf.Min(shopState.Offers.Count, handSize);
            for (int index = 0; index < renderCount; index++)
            {
                ShopOfferState offer = shopState.Offers[index];
                UnitDataSO unit = allUnits.FirstOrDefault(candidate => candidate.unitKey == offer.UnitKey);
                if (unit == null)
                {
                    Debug.LogWarning($"HandManager: Could not resolve UnitDataSO for unit key '{offer.UnitKey}'.");
                    continue;
                }

                Card card = CreateCard(unit, offer.OfferId, index);
                hand.Add(card);
                lastRenderedOfferIds.Add(offer.OfferId);
            }

            if (shouldStandUp)
            {
                cardHandDisplayer?.HandleRecentGenerationStandup();
            }
        }
    }
}
