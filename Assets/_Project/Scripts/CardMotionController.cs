using System;
using UnityEngine;

namespace CardSystem
{
    public class CardMotionController : MonoBehaviour
    {
        [Header("Scene Awareness")]
        [SerializeField, Tooltip("The Deck Manager with knowledge of the hand state.")]
        private HandManager handManager;

        [Header("Hover Settings")]
        [SerializeField, Tooltip("Distance the card lifts when hovered.")]
        private float hoverLiftDistance = 1.5f;
        [SerializeField, Tooltip("Speed of hover/return lerping.")]
        private float hoverLerpSpeed = 10f;

        [Header("Drag Settings")]
        [SerializeField, Tooltip("Total forward distance the card travels when dragged upward.")]
        private float dragForwardTravelDistance = 5.0f;
        [SerializeField, Tooltip("Maximum pixel distance for screen-based drag calculation.")]
        private float dragMaxScreenDistance = 200f;
        [SerializeField, Tooltip("Maximum rotation applied when dragged upward.")]
        private float dragTiltMaxDegrees = 40f;
        [SerializeField, Tooltip("Speed of return lerping when unhovering.")]
        private float rotationLerpSpeed = 5f;
        [SerializeField, Tooltip("Multiplicative factor applied to the hover lift when starting a drag.")]
        private float dragLiftDistance = 0.5f;
        [SerializeField, Tooltip("Speed of drag lift lerping.")]
        private float dragLiftLerpSpeed = 8f;
        [SerializeField, Tooltip("Distance the card will lower when another card is being dragged.")]
        private float loweredOffsetDist = 1.4f;

        [Header("Playable Zone")]
        [SerializeField, Tooltip("Forward drag percentage required to play the card.")]
        private float playableThreshold = 0.9f;

        [Header("Wobble Settings")]
        [SerializeField, Tooltip("How much mouse movement affects wobble.")]
        private float wobbleSensitivity = 0.5f;
        [SerializeField, Tooltip("How quickly wobble slows down.")]
        private float wobbleDampening = 20f;
        [SerializeField, Tooltip("Maximum degrees for wobble.")]
        private float wobbleMaxDegrees = 25f;

        // Runtime state
        private Camera mainCamera;
        private HandleCursor cursor;
        private CardState state;
        private UIBlockerManager uiBlockerManager;

        private Vector3 lastMousePos;
        private Vector3 dragStartMousePos;
        private Vector3 dragStartWorldPos;
        private Quaternion dragStartRotation;

        private float lastVerticalProgress = 0f;

        private Vector2 wobbleAngle = Vector2.zero;
        private Vector2 wobbleVelocity = Vector2.zero;

        private void Awake()
        {
            mainCamera = Camera.main;
            cursor = mainCamera.GetComponent<HandleCursor>();
            state = GetComponent<CardState>();
            uiBlockerManager = FindFirstObjectByType<UIBlockerManager>();
        }

        private void Update()
        {
            if (state.IsDragging)
            {
                UpdateDragPosition();
            }
            else if (state.IsHovering)
            {
                UpdateHoverMotion();
            }
            else
            {
                UpdateIdleMotion();
            }
        }

        private void UpdateIdleMotion()
        {
            Vector3 targetPos = state.HandAnchorPosition;

            if (handManager.IsHandLowered())
            {
                // Card's thin axis (downward from the face)
                Vector3 loweredOffset = -transform.forward * loweredOffsetDist;
                targetPos += loweredOffset;
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * hoverLerpSpeed); // returnSpeed ~5–10
            transform.rotation = Quaternion.Lerp(transform.rotation, state.HandAnchorRotation, Time.deltaTime * hoverLerpSpeed);
        }

        private void UpdateHoverMotion()
        {
            Vector3 liftDir = (mainCamera.transform.position - state.HandAnchorPosition).normalized;
            Vector3 liftedPos = state.HandAnchorPosition + liftDir * hoverLiftDistance;

            transform.position = Vector3.Lerp(transform.position, liftedPos, Time.deltaTime * hoverLerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, state.HandAnchorRotation, Time.deltaTime * rotationLerpSpeed);
        }

        public void SetHandManager(HandManager dm)
        {
            handManager = dm;
        }

        public void BeginDrag()
        {
            state.IsDragging = true;
            dragStartMousePos = Input.mousePosition;
            lastMousePos = dragStartMousePos;
            dragStartRotation = transform.rotation;

            // Step 1: Calculate hover position (already lifted)
            Vector3 liftDir = (mainCamera.transform.position - state.HandAnchorPosition).normalized;
            Vector3 hoverPos = state.HandAnchorPosition + liftDir * hoverLiftDistance;
            // Step 2: Add drag lift *on top* of hover
            dragStartWorldPos = hoverPos + liftDir * dragLiftDistance;

            lastVerticalProgress = 0f;

            // Reset wobble
            wobbleAngle = Vector2.zero;
            wobbleVelocity = Vector2.zero;
        }

        public void EndDrag()
        {
            state.IsDragging = false;
            wobbleVelocity = Vector2.zero;
            wobbleAngle = Vector2.zero;
            // transform.rotation = state.HandAnchorRotation;
        }

        private void UpdateDragPosition()
        {
            if (CheckIfInputIsBlocked())
                return;
            Vector3 currentMousePos = Input.mousePosition;

            Vector2 mouseDelta = (Vector2)(currentMousePos - lastMousePos);
            lastMousePos = currentMousePos;

            UpdateWobble(mouseDelta, state.IsDragging);

            // float verticalProgress = GetVerticalDragProgress(currentMousePos);
            lastVerticalProgress = GetVerticalDragProgress(currentMousePos);
            float horizontalProgress = GetHorizontalDragProgress(currentMousePos);

            ApplyDragTransform(lastVerticalProgress, horizontalProgress);
            // Optional:
            // if (percentDragged > 0.8f) ShowPlayPreview();
        }

        private void UpdateWobble(Vector2 mouseDelta, bool isDragging)
        {
            if (isDragging)
            {
                wobbleVelocity += new Vector2(mouseDelta.y, -mouseDelta.x) * wobbleSensitivity;
            }
            wobbleVelocity = Vector2.Lerp(wobbleVelocity, Vector2.zero, Time.deltaTime * wobbleDampening);

            wobbleAngle = Vector2.Lerp(wobbleAngle, wobbleAngle + wobbleVelocity, Time.deltaTime * 30f);
            wobbleAngle = Vector2.ClampMagnitude(wobbleAngle, wobbleMaxDegrees);
        }

        private float GetVerticalDragProgress(Vector3 currentMousePos)
        {
            float verticalDelta = currentMousePos.y - dragStartMousePos.y;
            return Mathf.Clamp01(verticalDelta / dragMaxScreenDistance);
        }

        private float GetHorizontalDragProgress(Vector3 currentMousePos)
        {
            float horizontalDelta = currentMousePos.x - dragStartMousePos.x;
            return Mathf.Clamp(horizontalDelta / dragMaxScreenDistance, -1f, 1f);
        }

        private void ApplyDragTransform(float forwardProgress, float horizontalProgress)
        {
            if (CheckIfInputIsBlocked())
                return;
            Vector3 forwardDir = mainCamera.transform.forward;
            Vector3 sideDir = mainCamera.transform.right;

            Vector3 forwardOffset = forwardDir * (dragForwardTravelDistance * forwardProgress);
            Vector3 sideOffset = sideDir * (dragForwardTravelDistance * 0.5f * horizontalProgress);

            Vector3 desiredPos = dragStartWorldPos + forwardOffset + sideOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * dragLiftLerpSpeed);

            float tiltAngle = forwardProgress * dragTiltMaxDegrees;

            // --- 🔧 Twist Correction (Y rotation only) ---
            float twistCorrectionFactor = Mathf.Clamp01(forwardProgress);
            float originalY = dragStartRotation.eulerAngles.y;
            float correctedY = Mathf.LerpAngle(originalY, mainCamera.transform.rotation.eulerAngles.y, twistCorrectionFactor);

            Quaternion targetRot = Quaternion.Euler(
                dragStartRotation.eulerAngles.x + tiltAngle + wobbleAngle.x,
                correctedY,
                dragStartRotation.eulerAngles.z + wobbleAngle.y
            );

            transform.rotation = targetRot;
        }

        private bool CheckIfInputIsBlocked()
        {
            bool isBlocked = uiBlockerManager.IsBlockingInput;
            if (isBlocked)
            {
                state.IsDragging = false;
                state.IsHovering = false;
            }
            return isBlocked;
        }

        private void OnMouseEnter()
        {
            if (CheckIfInputIsBlocked())
                return;
            bool canHover = true;
            if (handManager != null)
            {
                canHover = !handManager.IsCardBeingDragged(gameObject) && !handManager.IsHandLowered();
            }
            if (canHover)
            {
                state.IsHovering = true;
            }
            TryUpdateCursor();
        }

        private void OnMouseExit()
        {
            if (CheckIfInputIsBlocked())
                return;
            state.IsHovering = false;
            TryUpdateCursor();
        }

        void OnMouseDown()
        {
            if (CheckIfInputIsBlocked())
                return;
            handManager.OnCardDragChanged(gameObject, true);
            BeginDrag();
            state.IsDragging = true;
            TryUpdateCursor();
        }

        void OnMouseUp()
        {
            if (CheckIfInputIsBlocked())
                return;
            handManager.OnCardDragChanged(gameObject, false);
            EndDrag();
            state.IsDragging = false;

            if (lastVerticalProgress >= playableThreshold)
            {
                handManager?.PlayCard(gameObject);
            }
            TryUpdateCursor();
        }

        void OnMouseOver()
        {
            if (cursor.currentState != CursorState.HoverGrab && !handManager.IsCardBeingDragged(gameObject) && !CheckIfInputIsBlocked())
            {
                if (!handManager.IsCardBeingDragged())
                {
                    state.IsHovering = true;
                }
                TryUpdateCursor();
            }
        }

        private void TryUpdateCursor()
        {
            /*
                Enforces drag priority between all cards.
            */
            if (state.IsDragging)
                cursor.SetState(CursorState.Grab);
            else if (state.IsHovering)
                cursor.SetState(CursorState.HoverGrab);
            else if (!handManager.IsCardBeingDragged(gameObject))
            {
                cursor.SetState(CursorState.Normal);
            }
        }
    }
}