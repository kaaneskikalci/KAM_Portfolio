using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using KAM.Combat.TurnSystem;
using KAM.Core.Combat;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Displays the turn order bar at the top of the screen.
    /// Shows character portraits in order of upcoming turns.
    /// Based on GDD 1.7.2 Turn Order System.
    /// </summary>
    public class TurnOrderUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform portraitContainer;
        [SerializeField] private TurnPortraitSlot portraitSlotPrefab;

        [Header("Settings")]
        [SerializeField] private int maxVisibleSlots = 8;
        [SerializeField] private float slotSpacing = 10f;
        [SerializeField] private float currentTurnScale = 1.2f;
        [SerializeField] private float animationDuration = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color playerColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color currentTurnHighlight = Color.yellow;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Runtime
        private TurnManager _turnManager;
        private List<TurnPortraitSlot> _activeSlots = new List<TurnPortraitSlot>();
        private Queue<TurnPortraitSlot> _slotPool = new Queue<TurnPortraitSlot>();
        private bool _isSubscribed = false;
        private bool _poolCreated = false;

        private void Start()
        {
            TrySubscribe();
        }

        private void Update()
        {
            // Keep trying to subscribe until successful
            if (!_isSubscribed)
            {
                TrySubscribe();
            }
        }

        private void TrySubscribe()
        {
            if (_turnManager == null)
            {
                _turnManager = TurnManager.Instance;
            }

            if (_turnManager != null && !_isSubscribed)
            {
                _turnManager.OnTurnOrderChanged += UpdateTurnOrder;
                _turnManager.OnTurnStart += HighlightCurrentTurn;
                _turnManager.OnCombatEnd += ClearTurnOrder;
                _isSubscribed = true;

                if (debugMode)
                    Debug.Log("[TurnOrderUI] Successfully subscribed to TurnManager!");

                // Create pool now that we're subscribed
                if (!_poolCreated)
                {
                    CreateSlotPool();
                }
            }
        }

        private void CreateSlotPool()
        {
            if (_poolCreated) return;

            if (portraitContainer == null)
            {
                if (debugMode)
                    Debug.LogWarning("[TurnOrderUI] Portrait Container not assigned!");
                return;
            }

            _poolCreated = true;

            for (int i = 0; i < maxVisibleSlots; i++)
            {
                CreatePooledSlot();
            }

            if (debugMode)
                Debug.Log($"[TurnOrderUI] Created {maxVisibleSlots} portrait slots");
        }

        private void OnDestroy()
        {
            if (_turnManager != null)
            {
                _turnManager.OnTurnOrderChanged -= UpdateTurnOrder;
                _turnManager.OnTurnStart -= HighlightCurrentTurn;
                _turnManager.OnCombatEnd -= ClearTurnOrder;
            }
        }

        #region Slot Pooling

        private TurnPortraitSlot CreatePooledSlot()
        {
            if (portraitContainer == null)
            {
                Debug.LogWarning("[TurnOrderUI] Missing container!");
                return null;
            }

            TurnPortraitSlot slot;

            // If prefab exists, use it; otherwise create dynamically
            if (portraitSlotPrefab != null)
            {
                slot = Instantiate(portraitSlotPrefab, portraitContainer);
            }
            else
            {
                // Create slot dynamically without prefab
                slot = CreateSlotDynamically();
            }

            if (slot == null) return null;

            slot.gameObject.SetActive(false);
            _slotPool.Enqueue(slot);
            return slot;
        }

        /// <summary>
        /// Create a portrait slot dynamically without a prefab
        /// </summary>
        private TurnPortraitSlot CreateSlotDynamically()
        {
            GameObject slotObj = new GameObject("TurnPortraitSlot");
            slotObj.transform.SetParent(portraitContainer, false);

            RectTransform rect = slotObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(70, 80);

            // Add the slot component
            TurnPortraitSlot slot = slotObj.AddComponent<TurnPortraitSlot>();

            // Create border
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(slotObj.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            Image borderImage = borderObj.AddComponent<Image>();
            borderImage.color = Color.white;

            // Create portrait
            GameObject portraitObj = new GameObject("Portrait");
            portraitObj.transform.SetParent(slotObj.transform, false);
            RectTransform portraitRect = portraitObj.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.1f, 0.25f);
            portraitRect.anchorMax = new Vector2(0.9f, 0.95f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            Image portraitImage = portraitObj.AddComponent<Image>();
            portraitImage.color = Color.gray;

            // Create highlight
            GameObject highlightObj = new GameObject("Highlight");
            highlightObj.transform.SetParent(slotObj.transform, false);
            RectTransform highlightRect = highlightObj.AddComponent<RectTransform>();
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = new Vector2(-3, -3);
            highlightRect.offsetMax = new Vector2(3, 3);
            Image highlightImage = highlightObj.AddComponent<Image>();
            highlightImage.color = Color.yellow;
            highlightObj.SetActive(false);

            // Create order number text
            GameObject orderObj = new GameObject("OrderNumber");
            orderObj.transform.SetParent(slotObj.transform, false);
            RectTransform orderRect = orderObj.AddComponent<RectTransform>();
            orderRect.anchorMin = new Vector2(0, 1);
            orderRect.anchorMax = new Vector2(0.4f, 1);
            orderRect.pivot = new Vector2(0, 1);
            orderRect.anchoredPosition = Vector2.zero;
            orderRect.sizeDelta = new Vector2(0, 20);
            var orderText = orderObj.AddComponent<TMPro.TextMeshProUGUI>();
            orderText.text = "1";
            orderText.fontSize = 14;
            orderText.alignment = TMPro.TextAlignmentOptions.Center;
            orderText.color = Color.white;

            // Create unit name text
            GameObject nameObj = new GameObject("UnitName");
            nameObj.transform.SetParent(slotObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 0.25f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
            nameText.text = "Unit";
            nameText.fontSize = 10;
            nameText.alignment = TMPro.TextAlignmentOptions.Center;
            nameText.color = Color.white;

            // Setup references via reflection or public method
            slot.SetupReferences(portraitImage, borderImage, highlightImage, orderText, nameText);

            if (debugMode)
                Debug.Log("[TurnOrderUI] Created portrait slot dynamically");

            return slot;
        }

        private TurnPortraitSlot GetSlotFromPool()
        {
            TurnPortraitSlot slot;

            if (_slotPool.Count > 0)
            {
                slot = _slotPool.Dequeue();
            }
            else
            {
                slot = CreatePooledSlot();
                if (slot != null)
                {
                    _slotPool.Dequeue(); // Remove from pool since we just added it
                }
            }

            if (slot != null)
            {
                slot.gameObject.SetActive(true);
            }

            return slot;
        }

        private void ReturnSlotToPool(TurnPortraitSlot slot)
        {
            if (slot == null) return;

            slot.gameObject.SetActive(false);
            slot.transform.SetAsLastSibling();
            _slotPool.Enqueue(slot);
        }

        #endregion

        #region Turn Order Display

        private void UpdateTurnOrder(List<CombatUnit> turnOrder)
        {
            // Return all active slots to pool
            foreach (var slot in _activeSlots)
            {
                ReturnSlotToPool(slot);
            }
            _activeSlots.Clear();

            // Create slots for visible units
            int displayCount = Mathf.Min(turnOrder.Count, maxVisibleSlots);

            for (int i = 0; i < displayCount; i++)
            {
                CombatUnit unit = turnOrder[i];
                TurnPortraitSlot slot = GetSlotFromPool();

                if (slot != null)
                {
                    Color borderColor = unit.IsPlayer ? playerColor : enemyColor;
                    bool isCurrentTurn = (i == 0);

                    slot.Setup(unit, borderColor, isCurrentTurn, i + 1);

                    if (isCurrentTurn)
                    {
                        slot.SetScale(currentTurnScale);
                        slot.SetHighlight(currentTurnHighlight);
                    }
                    else
                    {
                        slot.SetScale(1f);
                        slot.ClearHighlight();
                    }

                    slot.transform.SetSiblingIndex(i);
                    _activeSlots.Add(slot);
                }
            }
        }

        private void HighlightCurrentTurn(CombatUnit currentUnit)
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                var slot = _activeSlots[i];
                bool isCurrent = (slot.Unit == currentUnit);

                if (isCurrent)
                {
                    slot.SetScale(currentTurnScale);
                    slot.SetHighlight(currentTurnHighlight);
                    slot.PlayTurnStartAnimation();
                }
                else
                {
                    slot.SetScale(1f);
                    slot.ClearHighlight();
                }
            }
        }

        private void ClearTurnOrder()
        {
            foreach (var slot in _activeSlots)
            {
                ReturnSlotToPool(slot);
            }
            _activeSlots.Clear();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Force refresh the turn order display
        /// </summary>
        public void RefreshDisplay()
        {
            if (_turnManager != null)
            {
                UpdateTurnOrder(_turnManager.GetUpcomingTurns());
            }
        }

        /// <summary>
        /// Get the slot for a specific unit
        /// </summary>
        public TurnPortraitSlot GetSlotForUnit(CombatUnit unit)
        {
            return _activeSlots.Find(s => s.Unit == unit);
        }

        #endregion
    }

}
