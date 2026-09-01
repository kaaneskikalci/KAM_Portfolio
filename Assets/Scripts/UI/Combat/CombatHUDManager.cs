using UnityEngine;
using KAM.Combat.TurnSystem;
using KAM.Core.Combat;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Central manager for all combat UI elements.
    /// Shows/hides UI based on combat state and coordinates updates.
    /// </summary>
    public class CombatHUDManager : MonoBehaviour
    {
        private static CombatHUDManager _instance;
        public static CombatHUDManager Instance => _instance;

        [Header("UI References")]
        [SerializeField] private GameObject combatHUDRoot;
        [SerializeField] private TurnOrderUI turnOrderUI;
        [SerializeField] private ActionMenuUI actionMenuUI;

        [Header("Settings")]
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true; // Enable by default for testing

        // References
        private TurnManager _turnManager;
        private BattleManager _battleManager;
        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;
        private bool _isSubscribed = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _canvasGroup = combatHUDRoot?.GetComponent<CanvasGroup>();
            if (_canvasGroup == null && combatHUDRoot != null)
            {
                _canvasGroup = combatHUDRoot.AddComponent<CanvasGroup>();
            }
        }

        private void Start()
        {
            // Try to find managers immediately
            TrySubscribeToManagers();

            if (hideOnStart)
            {
                HideImmediate();
            }
        }

        private void Update()
        {
            // Keep trying to subscribe if not yet subscribed
            if (!_isSubscribed)
            {
                TrySubscribeToManagers();
            }
        }

        private void TrySubscribeToManagers()
        {
            // Try TurnManager first
            if (_turnManager == null)
            {
                _turnManager = TurnManager.Instance;
            }

            // Also try BattleManager as fallback
            if (_battleManager == null)
            {
                _battleManager = BattleManager.Instance;
            }

            // Subscribe to TurnManager if available
            if (_turnManager != null && !_isSubscribed)
            {
                SubscribeToTurnManager();
                _isSubscribed = true;

                if (debugMode)
                    Debug.Log("[CombatHUDManager] Successfully subscribed to TurnManager!");
            }
            // Fallback: Subscribe to BattleManager
            else if (_battleManager != null && !_isSubscribed)
            {
                SubscribeToBattleManager();
                _isSubscribed = true;

                if (debugMode)
                    Debug.Log("[CombatHUDManager] Subscribed to BattleManager (TurnManager fallback)");
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            UnsubscribeFromAll();
        }

        #region Event Subscription

        private void SubscribeToTurnManager()
        {
            if (_turnManager == null) return;

            _turnManager.OnCombatStart += HandleCombatStart;
            _turnManager.OnCombatEnd += HandleCombatEnd;
            _turnManager.OnTurnStart += HandleTurnStart;
            _turnManager.OnTurnEnd += HandleTurnEnd;
            _turnManager.OnStateChanged += HandleStateChanged;
        }

        private void SubscribeToBattleManager()
        {
            if (_battleManager == null) return;

            _battleManager.OnCombatStart += HandleCombatStart;
            _battleManager.OnCombatEnd += HandleCombatEnd;
            _battleManager.OnPlayerTurnStart += HandlePlayerTurnStart;
            _battleManager.OnTurnEnd += HandleAnyTurnEnd;
        }

        private void UnsubscribeFromAll()
        {
            if (_turnManager != null)
            {
                _turnManager.OnCombatStart -= HandleCombatStart;
                _turnManager.OnCombatEnd -= HandleCombatEnd;
                _turnManager.OnTurnStart -= HandleTurnStart;
                _turnManager.OnTurnEnd -= HandleTurnEnd;
                _turnManager.OnStateChanged -= HandleStateChanged;
            }

            if (_battleManager != null)
            {
                _battleManager.OnCombatStart -= HandleCombatStart;
                _battleManager.OnCombatEnd -= HandleCombatEnd;
                _battleManager.OnPlayerTurnStart -= HandlePlayerTurnStart;
                _battleManager.OnTurnEnd -= HandleAnyTurnEnd;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleCombatStart()
        {
            if (debugMode)
                Debug.Log("[CombatHUDManager] Combat started - showing HUD");

            Show();
        }

        private void HandleCombatEnd()
        {
            if (debugMode)
                Debug.Log("[CombatHUDManager] Combat ended - hiding HUD");

            Hide();
        }

        private void HandleTurnStart(CombatUnit unit)
        {
            if (debugMode)
                Debug.Log($"[CombatHUDManager] Turn started: {unit.UnitName}");

            // Show action menu only for player turn
            if (unit.IsPlayer && actionMenuUI != null)
            {
                actionMenuUI.Show();
            }
            else if (actionMenuUI != null)
            {
                actionMenuUI.Hide();
            }
        }

        private void HandleTurnEnd(CombatUnit unit)
        {
            if (debugMode)
                Debug.Log($"[CombatHUDManager] Turn ended: {unit.UnitName}");

            // Hide action menu when turn ends
            if (actionMenuUI != null)
            {
                actionMenuUI.Hide();
            }
        }

        // BattleManager fallback handlers
        private void HandlePlayerTurnStart()
        {
            if (debugMode)
                Debug.Log("[CombatHUDManager] Player turn started");

            if (actionMenuUI != null)
            {
                actionMenuUI.Show();
            }
        }

        private void HandleAnyTurnEnd()
        {
            if (actionMenuUI != null)
            {
                actionMenuUI.Hide();
            }
        }

        private void HandleStateChanged(CombatState state)
        {
            if (debugMode)
                Debug.Log($"[CombatHUDManager] State changed: {state}");

            // Could add state-specific UI updates here
            switch (state)
            {
                case CombatState.Animating:
                    // Optionally disable UI interaction during animations
                    if (actionMenuUI != null)
                        actionMenuUI.SetInteractable(false);
                    break;

                case CombatState.PlayerTurn:
                    if (actionMenuUI != null)
                        actionMenuUI.SetInteractable(true);
                    break;

                case CombatState.EnemyTurn:
                    if (actionMenuUI != null)
                        actionMenuUI.SetInteractable(false);
                    break;
            }
        }

        #endregion

        #region Show/Hide

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            if (debugMode)
                Debug.Log("[CombatHUDManager] Showing Combat HUD!");

            if (combatHUDRoot != null)
            {
                combatHUDRoot.SetActive(true);
            }

            if (_canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration));
            }
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;

            if (debugMode)
                Debug.Log("[CombatHUDManager] Hiding Combat HUD!");

            if (_canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeAndDeactivate(fadeOutDuration));
            }
            else if (combatHUDRoot != null)
            {
                combatHUDRoot.SetActive(false);
            }
        }

        public void HideImmediate()
        {
            _isVisible = false;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            if (combatHUDRoot != null)
            {
                combatHUDRoot.SetActive(false);
            }
        }

        private System.Collections.IEnumerator FadeCanvasGroup(float from, float to, float duration)
        {
            if (_canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        private System.Collections.IEnumerator FadeAndDeactivate(float duration)
        {
            yield return FadeCanvasGroup(_canvasGroup.alpha, 0f, duration);

            if (combatHUDRoot != null)
            {
                combatHUDRoot.SetActive(false);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize HUD manager with references if not set in inspector
        /// </summary>
        public void Initialize(TurnOrderUI turnOrder, ActionMenuUI actionMenu)
        {
            turnOrderUI = turnOrder;
            actionMenuUI = actionMenu;

            // Force re-subscribe
            _isSubscribed = false;
            TrySubscribeToManagers();
        }

        /// <summary>
        /// Get reference to turn order UI
        /// </summary>
        public TurnOrderUI GetTurnOrderUI() => turnOrderUI;

        /// <summary>
        /// Get reference to action menu UI
        /// </summary>
        public ActionMenuUI GetActionMenuUI() => actionMenuUI;

        /// <summary>
        /// Force show HUD (for testing)
        /// </summary>
        public void ForceShow()
        {
            _isVisible = false; // Reset so Show() works
            Show();
        }

        #endregion
    }
}
