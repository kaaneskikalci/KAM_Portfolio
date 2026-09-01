using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using KAM.Core.Combat;
using KAM.Combat.TurnSystem;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Displays a scrollable log of combat actions.
    /// Shows damage dealt, healing, status effects, and turn information.
    /// </summary>
    public class CombatLogUI : MonoBehaviour
    {
        public static CombatLogUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject logPanel;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform logContentContainer;
        [SerializeField] private TextMeshProUGUI logEntryPrefab;

        [Header("Settings")]
        [SerializeField] private int maxEntries = 50;
        [SerializeField] private bool autoScroll = true;
        [SerializeField] private float fadeDelay = 5f;
        [SerializeField] private bool showTimestamps = false;

        [Header("Colors")]
        [SerializeField] private Color playerDamageColor = new Color(0.3f, 1f, 0.3f); // Green - damage TO enemies
        [SerializeField] private Color enemyDamageColor = new Color(1f, 0.3f, 0.3f); // Red - damage TO player
        [SerializeField] private Color healColor = new Color(0.3f, 1f, 0.5f); // Light green
        [SerializeField] private Color buffColor = new Color(0.3f, 0.5f, 1f); // Blue
        [SerializeField] private Color debuffColor = new Color(0.8f, 0.3f, 0.8f); // Purple
        [SerializeField] private Color turnColor = new Color(1f, 1f, 0.5f); // Yellow
        [SerializeField] private Color neutralColor = Color.white;
        [SerializeField] private Color criticalColor = new Color(1f, 0.8f, 0f); // Gold

        // Runtime
        private List<TextMeshProUGUI> _logEntries = new List<TextMeshProUGUI>();
        private TurnManager _turnManager;
        private int _currentRound = 0;
        private bool _isSubscribedToTurnManager = false;
        private bool _combatStartLogged = false;
        private bool _isCombatLogActive = false;
        private CanvasGroup _panelCanvasGroup;
        private const string RuntimeCanvasName = "CombatLogCanvas";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Create content container if not assigned
            if (logContentContainer == null && scrollRect != null)
            {
                logContentContainer = scrollRect.content;
            }

            EnsureRuntimeLayout();
        }

        private void OnEnable()
        {
            TrySubscribeToTurnManager();

            if (_turnManager != null && _turnManager.IsInCombat)
            {
                ShowForActiveCombat(false);
            }
        }

        private void Start()
        {
            TrySubscribeToTurnManager();

            // Subscribe to all combat units
            UnsubscribeFromCombatUnits();
            SubscribeToCombatUnits();

            if (_turnManager != null && _turnManager.IsInCombat)
            {
                ShowForActiveCombat(true);
            }
            else if (logPanel != null)
            {
                _isCombatLogActive = false;
                SetLogPanelVisible(false);
            }
        }

        private void Update()
        {
            if (_isSubscribedToTurnManager) return;

            TrySubscribeToTurnManager();

            if (_turnManager != null && _turnManager.IsInCombat)
            {
                ShowForActiveCombat(true);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromTurnManager();

            UnsubscribeFromCombatUnits();

            if (Instance == this)
                Instance = null;
        }

        private void TrySubscribeToTurnManager()
        {
            TurnManager currentTurnManager = TurnManager.Instance;
            if (_isSubscribedToTurnManager)
            {
                if (_turnManager != null && _turnManager == currentTurnManager)
                    return;

                UnsubscribeFromTurnManager();
            }

            _turnManager = currentTurnManager;
            if (_turnManager == null) return;

            _turnManager.OnCombatStart += HandleCombatStart;
            _turnManager.OnCombatEnd += HandleCombatEnd;
            _turnManager.OnRoundStart += HandleRoundStart;
            _turnManager.OnTurnStart += HandleTurnStart;
            _isSubscribedToTurnManager = true;
        }

        private void UnsubscribeFromTurnManager()
        {
            if (!_isSubscribedToTurnManager || _turnManager == null) return;

            _turnManager.OnCombatStart -= HandleCombatStart;
            _turnManager.OnCombatEnd -= HandleCombatEnd;
            _turnManager.OnRoundStart -= HandleRoundStart;
            _turnManager.OnTurnStart -= HandleTurnStart;
            _isSubscribedToTurnManager = false;
        }

        private void SetLogPanelVisible(bool visible)
        {
            EnsureRuntimeLayout();

            if (logPanel == null) return;

            EnsureHierarchyActive(logPanel.transform);
            logPanel.transform.SetAsLastSibling();

            // Dynamically-created logs put this component on the same object as the panel.
            // Keep that object active so Update can keep trying to subscribe to TurnManager.
            if (logPanel == gameObject)
            {
                if (!logPanel.activeSelf)
                {
                    logPanel.SetActive(true);
                }

                if (_panelCanvasGroup == null)
                {
                    _panelCanvasGroup = logPanel.GetComponent<CanvasGroup>();
                    if (_panelCanvasGroup == null)
                    {
                        _panelCanvasGroup = logPanel.AddComponent<CanvasGroup>();
                    }
                }

                _panelCanvasGroup.alpha = visible ? 1f : 0f;
                _panelCanvasGroup.interactable = visible;
                _panelCanvasGroup.blocksRaycasts = visible;
                return;
            }

            logPanel.SetActive(visible);

            if (visible)
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void EnsureHierarchyActive(Transform target)
        {
            if (target == null)
                return;

            Transform current = target;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    current.gameObject.SetActive(true);

                current = current.parent;
            }
        }

        private void SubscribeToCombatUnits()
        {
            var units = FindObjectsByType<CombatUnit>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                unit.OnDamageDealt += HandleDamageDealt;
                unit.OnDamageTaken += HandleDamageTaken;
                unit.OnHealed += HandleHealed;
                unit.OnDeath += HandleDeath;
                unit.OnEffectAdded += HandleEffectAdded;
                unit.OnEffectRemoved += HandleEffectRemoved;
            }
        }

        private void UnsubscribeFromCombatUnits()
        {
            var units = FindObjectsByType<CombatUnit>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                unit.OnDamageDealt -= HandleDamageDealt;
                unit.OnDamageTaken -= HandleDamageTaken;
                unit.OnHealed -= HandleHealed;
                unit.OnDeath -= HandleDeath;
                unit.OnEffectAdded -= HandleEffectAdded;
                unit.OnEffectRemoved -= HandleEffectRemoved;
            }
        }

        #region Event Handlers

        private void HandleCombatStart()
        {
            ShowForActiveCombat(true);
        }

        public void ForceShowForCombat(bool logCombatStart = true)
        {
            TrySubscribeToTurnManager();
            ShowForActiveCombat(logCombatStart);
        }

        private void ShowForActiveCombat(bool logCombatStart)
        {
            EnsureRuntimeLayout();

            // Re-subscribe to combat units (they may not have existed at Start)
            UnsubscribeFromCombatUnits();
            SubscribeToCombatUnits();

            if (logCombatStart && !_combatStartLogged)
            {
                ClearLog();
            }

            _isCombatLogActive = true;
            SetLogPanelVisible(true);

            if (logCombatStart && !_combatStartLogged)
            {
                _combatStartLogged = true;
                LogMessage("--- Combat Started ---", turnColor);
            }
        }

        private void HandleCombatEnd()
        {
            _isCombatLogActive = false;
            _combatStartLogged = false;
            UnsubscribeFromCombatUnits();
            ClearLog();
            SetLogPanelVisible(false);
        }

        private void HandleRoundStart(int round)
        {
            _currentRound = round;
            LogMessage($"=== Round {round} ===", turnColor);
        }

        private void HandleTurnStart(CombatUnit unit)
        {
            if (unit.IsPlayer)
            {
                LogMessage($"Your turn", neutralColor);
            }
            else
            {
                LogMessage($"{unit.UnitName}'s turn", neutralColor);
            }
        }

        private void HandleDamageDealt(CombatUnit attacker, DamageResult result)
        {
            // This is called on the attacker, damage info has target
            // We use OnDamageTaken instead for cleaner logging
        }

        private void HandleDamageTaken(CombatUnit target, DamageResult result)
        {
            if (result.HitSuccessful)
            {
                string attackerName = result.DamageInfo.Source?.UnitName ?? "Unknown";
                string targetName = target.UnitName;
                int damage = result.DamageInfo.FinalDamage;
                bool isCrit = result.DamageInfo.IsCritical;

                // Determine color based on who took damage
                Color color = target.IsPlayer ? enemyDamageColor : playerDamageColor;
                if (isCrit)
                    color = criticalColor;

                string critText = isCrit ? " CRITICAL!" : "";
                LogMessage($"{attackerName} dealt {damage} damage to {targetName}{critText}", color);
            }
        }

        private void HandleHealed(CombatUnit unit, int amount)
        {
            LogMessage($"{unit.UnitName} healed for {amount} HP", healColor);
        }

        private void HandleDeath(CombatUnit unit)
        {
            Color color = unit.IsPlayer ? enemyDamageColor : playerDamageColor;
            LogMessage($"{unit.UnitName} was defeated!", color);
        }

        private void HandleEffectAdded(StatusEffect effect)
        {
            Color color = effect.EffectType == StatusEffectType.Buff ? buffColor : debuffColor;
            string targetName = effect.Target?.UnitName ?? "Unknown";
            LogMessage($"{targetName} gained {effect.EffectName}", color);
        }

        private void HandleEffectRemoved(StatusEffect effect)
        {
            Color color = effect.EffectType == StatusEffectType.Buff ? buffColor : debuffColor;
            string targetName = effect.Target?.UnitName ?? "Unknown";
            LogMessage($"{effect.EffectName} wore off on {targetName}", color);
        }

        #endregion

        #region Logging

        /// <summary>
        /// Add a message to the combat log.
        /// </summary>
        public void LogMessage(string message, Color color)
        {
            TrySubscribeToTurnManager();
            if (!_isCombatLogActive && (_turnManager == null || !_turnManager.IsInCombat))
                return;

            EnsureRuntimeLayout();
            SetLogPanelVisible(true);

            TextMeshProUGUI entry = CreateLogEntry();

            string timestamp = showTimestamps ? $"[{Time.time:F1}] " : "";
            entry.text = timestamp + message;
            entry.color = color;

            // Remove old entries if over limit
            while (_logEntries.Count > maxEntries)
            {
                TextMeshProUGUI oldest = _logEntries[0];
                _logEntries.RemoveAt(0);
                Destroy(oldest.gameObject);
            }

            // Auto-scroll to bottom
            if (autoScroll && scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// Log damage dealt.
        /// </summary>
        public void LogDamage(string attacker, string target, int damage, bool isCrit = false, bool isPlayerDamage = true)
        {
            Color color = isPlayerDamage ? playerDamageColor : enemyDamageColor;
            if (isCrit) color = criticalColor;

            string critText = isCrit ? " CRITICAL!" : "";
            LogMessage($"{attacker} dealt {damage} damage to {target}{critText}", color);
        }

        /// <summary>
        /// Log healing.
        /// </summary>
        public void LogHeal(string unitName, int amount)
        {
            LogMessage($"{unitName} healed for {amount} HP", healColor);
        }

        /// <summary>
        /// Log status effect.
        /// </summary>
        public void LogStatus(string unitName, string effectName, bool isApplied, bool isBuff)
        {
            Color color = isBuff ? buffColor : debuffColor;
            string action = isApplied ? "gained" : "lost";
            LogMessage($"{unitName} {action} {effectName}", color);
        }

        /// <summary>
        /// Log turn start.
        /// </summary>
        public void LogTurn(string unitName, bool isPlayer)
        {
            LogMessage($"{unitName}'s turn", turnColor);
        }

        /// <summary>
        /// Log a custom message.
        /// </summary>
        public void Log(string message)
        {
            LogMessage(message, neutralColor);
        }

        /// <summary>
        /// Clear all log entries.
        /// </summary>
        public void ClearLog()
        {
            foreach (var entry in _logEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            _logEntries.Clear();
        }

        private TextMeshProUGUI CreateLogEntry()
        {
            EnsureRuntimeLayout();

            TextMeshProUGUI entry;

            if (logEntryPrefab != null)
            {
                entry = Instantiate(logEntryPrefab, logContentContainer);
            }
            else
            {
                // Create text entry programmatically
                GameObject entryObj = new GameObject("LogEntry");
                entryObj.transform.SetParent(logContentContainer, false);

                entry = entryObj.AddComponent<TextMeshProUGUI>();
                entry.fontSize = 12;
                entry.alignment = TextAlignmentOptions.Left;

                // Add layout element for proper sizing
                LayoutElement layout = entryObj.AddComponent<LayoutElement>();
                layout.minHeight = 16;
                layout.flexibleWidth = 1;
            }

            _logEntries.Add(entry);
            return entry;
        }

        private void EnsureRuntimeLayout()
        {
            Canvas canvas = GetOrCreateRuntimeCanvas();

            if (logPanel == null)
            {
                bool componentIsPanel = GetComponent<RectTransform>() != null && GetComponent<Image>() != null;
                logPanel = componentIsPanel ? gameObject : new GameObject("CombatLogUI_Panel");

                if (!componentIsPanel)
                {
                    logPanel.transform.SetParent(canvas.transform, false);
                }
            }
            else if (logPanel.transform.parent == null)
            {
                logPanel.transform.SetParent(canvas.transform, false);
            }
            else if (logPanel.transform.parent != canvas.transform && Application.isPlaying)
            {
                logPanel.transform.SetParent(canvas.transform, false);
            }

            RectTransform panelRect = logPanel.GetComponent<RectTransform>();
            if (panelRect == null)
            {
                panelRect = logPanel.AddComponent<RectTransform>();
            }

            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0.3f, 0.4f);
            panelRect.offsetMin = new Vector2(10, 10);
            panelRect.offsetMax = new Vector2(-10, -10);
            panelRect.localScale = Vector3.one;

            Image background = logPanel.GetComponent<Image>();
            if (background == null)
            {
                background = logPanel.AddComponent<Image>();
            }
            background.color = new Color(0, 0, 0, 0.7f);

            if (_panelCanvasGroup == null)
            {
                _panelCanvasGroup = logPanel.GetComponent<CanvasGroup>();
                if (_panelCanvasGroup == null)
                {
                    _panelCanvasGroup = logPanel.AddComponent<CanvasGroup>();
                }
            }

            if (scrollRect == null)
            {
                Transform existingScroll = logPanel.transform.Find("ScrollView");
                GameObject scrollObj = existingScroll != null ? existingScroll.gameObject : new GameObject("ScrollView");
                scrollObj.transform.SetParent(logPanel.transform, false);

                RectTransform scrollTransform = scrollObj.GetComponent<RectTransform>();
                if (scrollTransform == null)
                {
                    scrollTransform = scrollObj.AddComponent<RectTransform>();
                }
                scrollTransform.anchorMin = Vector2.zero;
                scrollTransform.anchorMax = Vector2.one;
                scrollTransform.offsetMin = new Vector2(5, 5);
                scrollTransform.offsetMax = new Vector2(-5, -5);

                scrollRect = scrollObj.GetComponent<ScrollRect>();
                if (scrollRect == null)
                {
                    scrollRect = scrollObj.AddComponent<ScrollRect>();
                }
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            if (scrollRect.viewport == null)
            {
                Transform existingViewport = scrollRect.transform.Find("Viewport");
                GameObject viewportObj = existingViewport != null ? existingViewport.gameObject : new GameObject("Viewport");
                viewportObj.transform.SetParent(scrollRect.transform, false);

                RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
                if (viewportRect == null)
                {
                    viewportRect = viewportObj.AddComponent<RectTransform>();
                }
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;

                Image viewportMask = viewportObj.GetComponent<Image>();
                if (viewportMask == null)
                {
                    viewportMask = viewportObj.AddComponent<Image>();
                }
                viewportMask.color = Color.white;

                Mask mask = viewportObj.GetComponent<Mask>();
                if (mask == null)
                {
                    mask = viewportObj.AddComponent<Mask>();
                }
                mask.showMaskGraphic = false;

                scrollRect.viewport = viewportRect;
            }

            if (logContentContainer == null)
            {
                Transform existingContent = scrollRect.viewport.transform.Find("Content");
                GameObject contentObj = existingContent != null ? existingContent.gameObject : new GameObject("Content");
                contentObj.transform.SetParent(scrollRect.viewport.transform, false);

                RectTransform contentRect = contentObj.GetComponent<RectTransform>();
                if (contentRect == null)
                {
                    contentRect = contentObj.AddComponent<RectTransform>();
                }
                contentRect.anchorMin = new Vector2(0, 1);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;

                VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
                if (layout == null)
                {
                    layout = contentObj.AddComponent<VerticalLayoutGroup>();
                }
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 2;
                layout.padding = new RectOffset(5, 5, 5, 5);

                ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = contentObj.AddComponent<ContentSizeFitter>();
                }
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                logContentContainer = contentObj.transform;
                scrollRect.content = contentRect;
            }
            else if (scrollRect.content == null)
            {
                scrollRect.content = logContentContainer.GetComponent<RectTransform>();
            }

            logPanel.transform.SetAsLastSibling();
        }

        #endregion

        #region Static Setup

        /// <summary>
        /// Create the combat log UI if it doesn't exist.
        /// </summary>
        public static CombatLogUI CreateInstance()
        {
            if (Instance != null)
            {
                Instance.EnsureRuntimeLayout();
                return Instance;
            }

            Canvas canvas = GetOrCreateRuntimeCanvas();

            // Create log panel
            GameObject panelObj = new GameObject("CombatLogUI");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0.3f, 0.4f);
            panelRect.offsetMin = new Vector2(10, 10);
            panelRect.offsetMax = new Vector2(-10, -10);

            // Background
            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);

            // Create scroll view
            GameObject scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(panelObj.transform, false);

            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(5, 5);
            scrollRect.offsetMax = new Vector2(-5, -5);

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // Create viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);

            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportMask = viewportObj.AddComponent<Image>();
            viewportMask.color = Color.white;
            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            scroll.viewport = viewportRect;

            // Create content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2;
            layout.padding = new RectOffset(5, 5, 5, 5);

            ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;

            // Add component
            CombatLogUI logUI = panelObj.AddComponent<CombatLogUI>();
            logUI.logPanel = panelObj;
            logUI.scrollRect = scroll;
            logUI.logContentContainer = contentObj.transform;

            return logUI;
        }

        private static Canvas GetOrCreateRuntimeCanvas()
        {
            GameObject canvasObj = GameObject.Find(RuntimeCanvasName);
            Canvas canvas = canvasObj != null ? canvasObj.GetComponent<Canvas>() : null;

            if (canvas == null)
            {
                canvasObj = new GameObject(RuntimeCanvasName);
                canvas = canvasObj.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObj.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            if (!canvasObj.activeSelf)
            {
                canvasObj.SetActive(true);
            }

            return canvas;
        }

        private static Canvas FindScreenSpaceCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                    return c;
            }
            return null;
        }

        #endregion
    }
}
