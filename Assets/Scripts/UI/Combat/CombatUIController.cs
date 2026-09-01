using UnityEngine;
using System.Collections.Generic;
using KAM.Combat.TurnSystem;
using KAM.Core;
using KAM.Core.Combat;
using KAM.Combat.TimedHit;
using KAM.Combat.Actions;
using KAM.SaveSystem;
using KAM.UI.Feedback;
using UnityEngine.UI;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Connects the Combat UI to the actual combat system.
    /// Handles attack execution, damage popups, and health bar updates.
    /// </summary>
    public class CombatUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ActionMenuUI actionMenuUI;
        [SerializeField] private DamagePopup damagePopupPrefab;
        [SerializeField] private HealthBarUI healthBarPrefab;

        [Header("Quick Time Event Settings")]
        [Tooltip("Enable the QTE/Timed Hit system")]
        [SerializeField] private bool enableQTE = true;

        [Tooltip("Chance (0-100%) for QTE to trigger on attack")]
        [SerializeField, Range(0f, 100f)] private float qteChance = 25f;

        [Tooltip("If true, QTE always triggers (ignores chance) - useful for testing")]
        [SerializeField] private bool alwaysTriggerQTE = false;

        [Header("Combat Settings")]
        [SerializeField] private int baseDamage = 10;
        [SerializeField] private bool spawnHealthBars = true;

        [Header("Target Selection")]
        [Tooltip("Layer mask for enemy detection. Leave at 0 to auto-detect from enemy GameObjects")]
        [SerializeField] private LayerMask enemyLayer;
        [Tooltip("Color of the ring under the selected enemy")]
        [SerializeField] private Color selectedRingColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        [Tooltip("Color of the ring when hovering over an enemy")]
        [SerializeField] private Color hoverRingColor = new Color(1f, 1f, 0.2f, 0.6f);

        [Header("Combo System")]
        [Tooltip("Damage multiplier added per consecutive attack (0.1 = +10%)")]
        [SerializeField] private float comboMultiplierPerHit = 0.1f;
        [Tooltip("Maximum damage multiplier from combos")]
        [SerializeField] private float comboMaxMultiplier = 2f;
        [Tooltip("Maximum combo count")]
        [SerializeField] private int comboMaxCount = 10;

        [Header("Combat UI Toggles")]
        [Tooltip("Enable enemy target selection by clicking")]
        [SerializeField] private bool enableTargetSelection = true;
        [Tooltip("Enable combo system and UI")]
        [SerializeField] private bool enableComboSystem = true;
        [Tooltip("Enable damage preview on hover")]
        [SerializeField] private bool enableDamagePreview = true;
        [Tooltip("Enable combat log panel")]
        [SerializeField] private bool enableCombatLog = true;
        [Tooltip("Enable enemy intent icons")]
        [SerializeField] private bool enableEnemyIntents = true;
        [Tooltip("Enable status effect icons")]
        [SerializeField] private bool enableStatusEffects = true;

        [Header("Screen Unit HUD")]
        [Tooltip("Move persistent HP/status/intent information from world-space unit labels into fixed screen HUD panels")]
        [SerializeField] private bool useScreenUnitHud = true;
        [SerializeField] private bool keepWorldUnitHealthBars = false;
        [SerializeField] private bool keepWorldStatusEffectIcons = false;
        [SerializeField] private bool keepWorldEnemyIntentIcons = false;
        [SerializeField] private CombatUnitHUDPanel targetHudPrefab;
        [SerializeField] private CombatUnitHUDPanel playerHudPrefab;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // References
        private TurnManager _turnManager;
        private BattleManager _battleManager;
        private TimedHitController _timedHitController;
        private TimedHitCombatBridge _timedHitBridge;
        private KAM.Combat.SkillCombatBridge _skillBridge;
        private CombatUnit _currentTarget;

        // Cached skill list for the current combat
        private List<CombatAction> _availableSkills = new List<CombatAction>();

        // Health bar tracking
        private Dictionary<CombatUnit, HealthBarUI> _healthBars = new Dictionary<CombatUnit, HealthBarUI>();

        // Enemy intent UI tracking
        private Dictionary<EnemyCombatUnit, EnemyIntentUI> _intentUIs = new Dictionary<EnemyCombatUnit, EnemyIntentUI>();

        // Status effect UI tracking
        private Dictionary<CombatUnit, StatusEffectUI> _statusEffectUIs = new Dictionary<CombatUnit, StatusEffectUI>();

        // Screen-space unit HUD
        private GameObject _screenUnitHudRoot;
        private CombatUnitHUDPanel _selectedEnemyHudPanel;
        private CombatUnitHUDPanel _playerHudPanel;
        private bool _targetSelectorSubscribed;

        private void Start()
        {
            // Find references
            _turnManager = TurnManager.Instance;
            _battleManager = BattleManager.Instance;
            _timedHitController = FindFirstObjectByType<TimedHitController>();
            _timedHitBridge = FindFirstObjectByType<TimedHitCombatBridge>();

            // Find ActionMenuUI if not assigned
            if (actionMenuUI == null)
            {
                actionMenuUI = FindFirstObjectByType<ActionMenuUI>();
            }

            // Ensure SkillCombatBridge exists
            _skillBridge = KAM.Combat.SkillCombatBridge.Instance;
            if (_skillBridge == null)
            {
                GameObject bridgeObj = new GameObject("SkillCombatBridge");
                _skillBridge = bridgeObj.AddComponent<KAM.Combat.SkillCombatBridge>();
                if (debugMode)
                    Debug.Log("[CombatUIController] Created SkillCombatBridge");
            }

            // Subscribe to UI events
            if (actionMenuUI != null)
            {
                actionMenuUI.OnAttackSelected += HandleAttackSelected;
                actionMenuUI.OnDefendSelected += HandleDefendSelected;
                actionMenuUI.OnSkillSelected += HandleSkillSelected;

                if (debugMode)
                    Debug.Log("[CombatUIController] Subscribed to ActionMenuUI events");
            }
            else
            {
                Debug.LogWarning("[CombatUIController] ActionMenuUI not found!");
            }

            // Load prefabs from Resources if not assigned
            if (damagePopupPrefab == null)
            {
                damagePopupPrefab = Resources.Load<DamagePopup>("Prefabs/DamagePopup");
            }

            if (healthBarPrefab == null)
            {
                var prefabGO = Resources.Load<GameObject>("Prefabs/HealthBarUI");
                if (prefabGO != null)
                    healthBarPrefab = prefabGO.GetComponent<HealthBarUI>();
            }

            // Subscribe to combat events to spawn health bars
            if (_turnManager != null)
            {
                _turnManager.OnCombatStart += SpawnHealthBarsForAllUnits;
                _turnManager.OnCombatEnd += CleanupHealthBars;
                _turnManager.OnTurnStart += HandleTurnStart;
            }

            // Bootstrap combat UI systems that need to exist before combat starts
            EnsureCombatUISystems();

            // If combat already started, spawn health bars now
            if (_turnManager != null && _turnManager.IsInCombat)
            {
                SpawnHealthBarsForAllUnits();
            }
        }

        private void OnDestroy()
        {
            if (actionMenuUI != null)
            {
                actionMenuUI.OnAttackSelected -= HandleAttackSelected;
                actionMenuUI.OnDefendSelected -= HandleDefendSelected;
                actionMenuUI.OnSkillSelected -= HandleSkillSelected;
            }

            if (_turnManager != null)
            {
                _turnManager.OnCombatStart -= SpawnHealthBarsForAllUnits;
                _turnManager.OnCombatEnd -= CleanupHealthBars;
                _turnManager.OnTurnStart -= HandleTurnStart;
            }

            UnsubscribeFromTargetSelector();
            CleanupHealthBars();
        }

        /// <summary>
        /// Handle turn start - trigger enemy AI if it's enemy's turn
        /// </summary>
        private void HandleTurnStart(CombatUnit unit)
        {
            if (unit == null) return;

            if (unit.IsEnemy)
            {
                // It's an enemy's turn - run AI
                if (debugMode)
                    Debug.Log($"[CombatUIController] Enemy turn: {unit.UnitName}");

                StartCoroutine(ExecuteEnemyTurn(unit));
            }
            else if (unit.IsPlayer)
            {
                // Show action menu for player turn (fallback if CombatHUDManager doesn't handle it)
                if (actionMenuUI != null)
                {
                    actionMenuUI.Show();
                }

                // Player's turn - populate skills submenu and update AP display
                PopulateSkillsForPlayer();

                // Update AP UI
                if (ActionPointUI.Instance != null)
                    ActionPointUI.Instance.UpdateDisplay();
            }

            RefreshScreenUnitHud();
        }

        /// <summary>
        /// Populate the skills submenu with unlocked active combat skills.
        /// </summary>
        private void PopulateSkillsForPlayer()
        {
            if (_skillBridge == null)
                _skillBridge = KAM.Combat.SkillCombatBridge.Instance;

            if (actionMenuUI == null) return;

            _availableSkills.Clear();

            // Populate skills from bridge
            if (_skillBridge != null)
            {
                _availableSkills = _skillBridge.GetAvailableCombatSkills();
            }
            actionMenuUI.PopulateSkills(_availableSkills);

            // Check player AP to enable/disable buttons
            PlayerCombatUnit player = _battleManager?.PlayerCombat;
            if (player == null)
                player = FindFirstObjectByType<PlayerCombatUnit>();

            bool hasAP = player != null && player.PlayerSpecificStats.HasAction;
            bool hasUnlockedSkills = _availableSkills.Count > 0;

            // Skills require 2 AP — check if any skill can actually be used
            int usableSkillCount = 0;
            if (player != null && _availableSkills.Count > 0)
            {
                foreach (var skill in _availableSkills)
                {
                    if (player.CanPerformAction(skill.ActionPointCost, skill.ActionPointCount) && !skill.IsOnCooldown)
                    {
                        usableSkillCount++;
                    }
                }
            }

            // Attack requires 1 AP. Skills panel only requires unlocked skills.
            // Defend is always available and is the intended way to bank AP.
            actionMenuUI.UpdateAvailability(hasAP, hasUnlockedSkills, false, true);

            if (debugMode)
            {
                int currentAP = player?.PlayerSpecificStats.CurrentActions ?? 0;
                int maxAP = player?.PlayerSpecificStats.MaxActions ?? 0;
                Debug.Log($"[CombatUIController] Player turn: {currentAP}/{maxAP} AP, {_availableSkills.Count} unlocked skills, {usableSkillCount} usable now");
            }
        }

        #region Combat UI System Bootstrap

        /// <summary>
        /// Ensures all combat UI subsystems exist. Creates them if missing.
        /// Called once during Start() so they're ready when combat begins.
        /// </summary>
        private void EnsureCombatUISystems()
        {
            // EnemyTargetSelector - allows clicking enemies to select attack target
            if (enableTargetSelection && EnemyTargetSelector.Instance == null)
            {
                GameObject selectorObj = new GameObject("EnemyTargetSelector");
                var selector = selectorObj.AddComponent<EnemyTargetSelector>();
                // Pass settings from CombatUIController Inspector
                selector.SetSettings(enemyLayer, selectedRingColor, hoverRingColor);
                if (debugMode)
                    Debug.Log("[CombatUIController] Created EnemyTargetSelector");
            }

            // ComboSystem - tracks consecutive attacks for damage multiplier
            if (enableComboSystem && KAM.Combat.ComboSystem.Instance == null)
            {
                GameObject comboObj = new GameObject("ComboSystem");
                var combo = comboObj.AddComponent<KAM.Combat.ComboSystem>();
                combo.SetSettings(comboMultiplierPerHit, comboMaxMultiplier, comboMaxCount);
                if (debugMode)
                    Debug.Log("[CombatUIController] Created ComboSystem");
            }

            // ComboUI - visual combo counter display
            if (enableComboSystem && ComboUI.Instance == null)
            {
                ComboUI.CreateInstance();
                if (debugMode)
                    Debug.Log("[CombatUIController] Created ComboUI");
            }

            // DamagePreviewUI - shows damage range when hovering enemies
            if (enableDamagePreview && DamagePreviewUI.Instance == null)
            {
                DamagePreviewUI.CreateInstance();
                if (debugMode)
                    Debug.Log("[CombatUIController] Created DamagePreviewUI");
            }

            // CombatLogUI - scrollable combat action history
            if (enableCombatLog && CombatLogUI.Instance == null)
            {
                CombatLogUI.CreateInstance();
                if (debugMode)
                    Debug.Log("[CombatUIController] Created CombatLogUI");
            }

            if (enableCombatLog && CombatLogUI.Instance != null && _turnManager != null && _turnManager.IsInCombat)
            {
                CombatLogUI.Instance.ForceShowForCombat(false);
            }

            // ActionPointUI - shows player's current AP as diamonds
            if (ActionPointUI.Instance == null)
                ActionPointUI.CreateInstance();
            else
                ActionPointUI.Instance.SubscribeToTurnEvents();

            if (debugMode && ActionPointUI.Instance != null)
            {
                Debug.Log("[CombatUIController] ActionPointUI ready");
            }
        }

        #endregion

        #region Health Bar Management

        private void SpawnHealthBarsForAllUnits()
        {
            if (enableCombatLog)
            {
                if (CombatLogUI.Instance == null)
                    CombatLogUI.CreateInstance();

                CombatLogUI.Instance?.ForceShowForCombat(true);
            }

            // Reset skill cooldowns at combat start
            if (_skillBridge != null)
                _skillBridge.ResetCooldowns();

            if (useScreenUnitHud)
            {
                EnsureScreenUnitHud();
                SubscribeToTargetSelector();
            }

            if (!spawnHealthBars && !useScreenUnitHud) return;

            if (debugMode)
                Debug.Log("[CombatUIController] Preparing combat unit HUD...");

            // Find all combat units
            var allUnits = FindObjectsByType<CombatUnit>(FindObjectsSortMode.None);

            foreach (var unit in allUnits)
            {
                if (unit.IsAlive)
                {
                    // Subscribe to death event
                    unit.OnDeath -= HandleUnitDeath;
                    unit.OnDeath += HandleUnitDeath;

                    if (spawnHealthBars && (!useScreenUnitHud || keepWorldUnitHealthBars) && !_healthBars.ContainsKey(unit))
                    {
                        SpawnHealthBarForUnit(unit);
                    }

                    // Spawn status effect UI for all units
                    if (enableStatusEffects && (!useScreenUnitHud || keepWorldStatusEffectIcons) && !_statusEffectUIs.ContainsKey(unit))
                    {
                        SpawnStatusEffectUIForUnit(unit);
                    }

                    // Spawn intent UI for enemies
                    EnemyCombatUnit enemyUnit = unit as EnemyCombatUnit;
                    if (enableEnemyIntents && (!useScreenUnitHud || keepWorldEnemyIntentIcons) && enemyUnit != null && !_intentUIs.ContainsKey(enemyUnit))
                    {
                        SpawnIntentUIForEnemy(enemyUnit);
                    }
                }
            }

            RefreshScreenUnitHud();
        }

        private void EnsureScreenUnitHud()
        {
            if (_screenUnitHudRoot != null)
            {
                return;
            }

            GameObject canvasObj = new GameObject("ScreenUnitHUDCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 42;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            _screenUnitHudRoot = canvasObj;

            _selectedEnemyHudPanel = CombatUnitHUDPanel.CreateFromPrefab(
                targetHudPrefab,
                canvasObj.transform,
                "Target",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -132f),
                new Vector2(560f, 92f));

            _playerHudPanel = CombatUnitHUDPanel.CreateFromPrefab(
                playerHudPrefab,
                canvasObj.transform,
                "Player",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-360f, 36f),
                new Vector2(320f, 92f));
        }

        private void SubscribeToTargetSelector()
        {
            if (_targetSelectorSubscribed || EnemyTargetSelector.Instance == null)
            {
                return;
            }

            EnemyTargetSelector.Instance.OnTargetChanged += HandleTargetChanged;
            _targetSelectorSubscribed = true;
        }

        private void UnsubscribeFromTargetSelector()
        {
            if (!_targetSelectorSubscribed || EnemyTargetSelector.Instance == null)
            {
                _targetSelectorSubscribed = false;
                return;
            }

            EnemyTargetSelector.Instance.OnTargetChanged -= HandleTargetChanged;
            _targetSelectorSubscribed = false;
        }

        private void HandleTargetChanged(EnemyCombatUnit target)
        {
            if (_selectedEnemyHudPanel != null)
            {
                _selectedEnemyHudPanel.Bind(target);
            }
        }

        private void RefreshScreenUnitHud()
        {
            if (!useScreenUnitHud || _screenUnitHudRoot == null)
            {
                return;
            }

            CombatUnit player = _battleManager?.PlayerCombat;
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCombatUnit>();
            }

            _playerHudPanel?.Bind(player);

            CombatUnit target = null;
            if (EnemyTargetSelector.Instance != null)
            {
                target = EnemyTargetSelector.Instance.GetTargetOrDefault();
            }

            if (target == null && _turnManager != null)
            {
                var enemies = _turnManager.GetAliveEnemies();
                if (enemies.Count > 0)
                {
                    target = enemies[0];
                }
            }

            _selectedEnemyHudPanel?.Bind(target);
            _selectedEnemyHudPanel?.Refresh();
            _playerHudPanel?.Refresh();
        }

        private void SpawnIntentUIForEnemy(EnemyCombatUnit enemy)
        {
            EnemyIntentUI intentUI = EnemyIntentUI.CreateForEnemy(enemy);
            _intentUIs[enemy] = intentUI;

            if (debugMode)
                Debug.Log($"[CombatUIController] Spawned intent UI for {enemy.UnitName}");
        }

        private void SpawnStatusEffectUIForUnit(CombatUnit unit)
        {
            StatusEffectUI statusUI = StatusEffectUI.CreateForUnit(unit);
            _statusEffectUIs[unit] = statusUI;

            if (debugMode)
                Debug.Log($"[CombatUIController] Spawned status effect UI for {unit.UnitName}");
        }

        private void SpawnHealthBarForUnit(CombatUnit unit)
        {
            if (healthBarPrefab == null)
            {
                // Create health bar dynamically if no prefab
                CreateHealthBarDynamically(unit);
                return;
            }

            var healthBar = Instantiate(healthBarPrefab, unit.transform.position + Vector3.up * 2.5f, Quaternion.identity);
            healthBar.Initialize(unit);
            _healthBars[unit] = healthBar;

            if (debugMode)
                Debug.Log($"[CombatUIController] Spawned health bar for {unit.UnitName}");
        }

        private void CreateHealthBarDynamically(CombatUnit unit)
        {
            // Create a simple world-space canvas with health bar
            GameObject canvasObj = new GameObject($"HealthBar_{unit.UnitName}");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200, 50);
            canvasRect.localScale = Vector3.one * 0.01f;

            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            HealthBarUI healthBar = canvasObj.AddComponent<HealthBarUI>();

            // Create background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.3f);
            bgRect.anchorMax = new Vector2(1, 0.7f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Create fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            var fillImage = fillObj.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.2f);
            fillImage.type = UnityEngine.UI.Image.Type.Filled;
            fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;

            // Create text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.3f);
            textRect.anchorMax = new Vector2(1, 0.7f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var healthText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            healthText.text = $"{unit.Stats.CurrentHealth}/{unit.Stats.MaxHealth}";
            healthText.fontSize = 18;
            healthText.alignment = TMPro.TextAlignmentOptions.Center;
            healthText.color = Color.white;

            // Setup references using reflection or direct assignment
            // For now, we'll just track position manually
            canvasObj.transform.position = unit.transform.position + Vector3.up * 2.5f;

            // Add a simple follow script
            var follower = canvasObj.AddComponent<HealthBarFollower>();
            follower.Initialize(unit, fillImage, healthText);

            _healthBars[unit] = healthBar;

            if (debugMode)
                Debug.Log($"[CombatUIController] Created dynamic health bar for {unit.UnitName}");
        }

        private void CleanupHealthBars()
        {
            foreach (var kvp in _healthBars)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _healthBars.Clear();

            // Also cleanup intent UIs and status effect UIs
            CleanupIntentUIs();
            CleanupStatusEffectUIs();
            CleanupScreenUnitHud();
        }

        private void CleanupIntentUIs()
        {
            foreach (var kvp in _intentUIs)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _intentUIs.Clear();
        }

        private void CleanupStatusEffectUIs()
        {
            foreach (var kvp in _statusEffectUIs)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _statusEffectUIs.Clear();
        }

        private void CleanupScreenUnitHud()
        {
            UnsubscribeFromTargetSelector();

            if (_selectedEnemyHudPanel != null)
            {
                _selectedEnemyHudPanel.Clear();
                _selectedEnemyHudPanel = null;
            }

            if (_playerHudPanel != null)
            {
                _playerHudPanel.Clear();
                _playerHudPanel = null;
            }

            if (_screenUnitHudRoot != null)
            {
                Destroy(_screenUnitHudRoot);
                _screenUnitHudRoot = null;
            }
        }

        #endregion

        #region Death Handling

        /// <summary>
        /// Handle unit death - destroy unit and check for combat end
        /// </summary>
        private void HandleUnitDeath(CombatUnit unit)
        {
            if (unit == null) return;

            if (debugMode)
                Debug.Log($"[CombatUIController] {unit.UnitName} has died!");

            // Remove health bar
            if (_healthBars.ContainsKey(unit))
            {
                if (_healthBars[unit] != null)
                {
                    Destroy(_healthBars[unit].gameObject);
                }
                _healthBars.Remove(unit);
            }

            // Remove status effect UI
            if (_statusEffectUIs.ContainsKey(unit))
            {
                if (_statusEffectUIs[unit] != null)
                {
                    Destroy(_statusEffectUIs[unit].gameObject);
                }
                _statusEffectUIs.Remove(unit);
            }

            // Start death sequence
            RefreshScreenUnitHud();
            StartCoroutine(DeathSequence(unit));
        }

        /// <summary>
        /// Disable all AI and movement components on a unit to stop it immediately
        /// </summary>
        private void DisableUnitAI(CombatUnit unit)
        {
            if (unit == null) return;

            // Disable EnemyStateMachine
            var stateMachine = unit.GetComponent<EnemyStateMachine>();
            if (stateMachine != null)
            {
                stateMachine.enabled = false;
                if (debugMode)
                    Debug.Log($"[CombatUIController] Disabled EnemyStateMachine on {unit.UnitName}");
            }

            // Disable EnemyMovement
            var enemyMovement = unit.GetComponent<EnemyMovement>();
            if (enemyMovement != null)
            {
                enemyMovement.enabled = false;
            }

            // Disable EnemyVision
            var enemyVision = unit.GetComponent<EnemyVision>();
            if (enemyVision != null)
            {
                enemyVision.enabled = false;
            }

            // Disable EnemyRoaming
            var enemyRoaming = unit.GetComponent<EnemyRoaming>();
            if (enemyRoaming != null)
            {
                enemyRoaming.enabled = false;
            }

            // Disable NavMeshAgent if present
            var navAgent = unit.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.isStopped = true;
                navAgent.enabled = false;
            }

            // Disable Rigidbody physics
            var rb = unit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
            }

            // Disable any CharacterController
            var charController = unit.GetComponent<CharacterController>();
            if (charController != null)
            {
                charController.enabled = false;
            }

            // Disable collider so it doesn't block anything
            var colliders = unit.GetComponents<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        /// <summary>
        /// Death sequence with visual effects
        /// </summary>
        private System.Collections.IEnumerator DeathSequence(CombatUnit unit)
        {
            if (unit == null) yield break;

            // IMMEDIATELY disable AI and movement components to stop chasing
            DisableUnitAI(unit);

            // Get renderers for fade effect
            var renderers = unit.GetComponentsInChildren<Renderer>();
            var spriteRenderers = unit.GetComponentsInChildren<SpriteRenderer>();

            float fadeDuration = 1f;
            float elapsed = 0f;

            // Store original colors
            var originalColors = new System.Collections.Generic.Dictionary<Renderer, Color>();
            var originalSpriteColors = new System.Collections.Generic.Dictionary<SpriteRenderer, Color>();

            foreach (var r in renderers)
            {
                if (r.material.HasProperty("_Color"))
                {
                    originalColors[r] = r.material.color;
                }
            }

            foreach (var sr in spriteRenderers)
            {
                originalSpriteColors[sr] = sr.color;
            }

            // Fade out effect
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

                foreach (var kvp in originalColors)
                {
                    if (kvp.Key != null && kvp.Key.material.HasProperty("_Color"))
                    {
                        Color c = kvp.Value;
                        c.a = alpha;
                        kvp.Key.material.color = c;
                    }
                }

                foreach (var kvp in originalSpriteColors)
                {
                    if (kvp.Key != null)
                    {
                        Color c = kvp.Value;
                        c.a = alpha;
                        kvp.Key.color = c;
                    }
                }

                yield return null;
            }

            // Check if it's player or enemy
            if (unit.IsPlayer)
            {
                HandlePlayerDeath(unit);
            }
            else
            {
                HandleEnemyDeath(unit);
            }
        }

        /// <summary>
        /// Handle player death - game over
        /// </summary>
        private void HandlePlayerDeath(CombatUnit player)
        {
            if (debugMode)
                Debug.Log("[CombatUIController] PLAYER DIED - GAME OVER!");

            // Hide action menu
            if (actionMenuUI != null)
            {
                actionMenuUI.Hide();
            }

            // End combat with defeat
            if (_turnManager != null)
            {
                _turnManager.EndCombat(CombatResult.Defeat);
            }

            DeathUI.ShowRespawnOrFallback();

            Debug.LogWarning("=== PLAYER DEFEATED: DEATH UI OPENED ===");
        }

        /// <summary>
        /// Handle enemy death - remove from map and check for victory
        /// </summary>
        private void HandleEnemyDeath(CombatUnit enemy)
        {
            if (debugMode)
                Debug.Log($"[CombatUIController] Enemy {enemy.UnitName} destroyed!");

            // If enemy is an EnemyCombatUnit, handle loot
            if (enemy is EnemyCombatUnit enemyUnit)
            {
                var player = FindFirstObjectByType<PlayerCombatUnit>();
                if (player != null)
                {
                    enemyUnit.HandleDeathAndDropLoot(player);
                }

                MonoBehaviour persistentBoss = FindPersistentBossState(enemyUnit);
                if (persistentBoss != null && enemyUnit.IsBoss)
                {
                    persistentBoss.SendMessage("MarkDefeated", SendMessageOptions.DontRequireReceiver);
                    SaveManager.Instance?.SaveGame();
                    StartCoroutine(CheckVictoryDelayed());
                    return;
                }
            }

            // Destroy enemy object
            EditorSelectionGuard.ClearSelectionIfTarget(enemy.gameObject);
            Destroy(enemy.gameObject);

            // Check if all enemies are dead (victory)
            StartCoroutine(CheckVictoryDelayed());
        }

        private MonoBehaviour FindPersistentBossState(EnemyCombatUnit enemyUnit)
        {
            if (enemyUnit == null)
                return null;

            foreach (MonoBehaviour behaviour in enemyUnit.GetComponents<MonoBehaviour>())
            {
                if (behaviour is ISaveable saveable && saveable.SaveableType == "PersistentBossState")
                    return behaviour;
            }

            return null;
        }

        /// <summary>
        /// Check for victory after a short delay (to let death animations complete)
        /// </summary>
        private System.Collections.IEnumerator CheckVictoryDelayed()
        {
            yield return new WaitForSeconds(0.5f);

            // Count remaining enemies
            var remainingEnemies = FindObjectsByType<EnemyCombatUnit>(FindObjectsSortMode.None);
            int aliveEnemies = 0;

            foreach (var enemy in remainingEnemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    aliveEnemies++;
                }
            }

            if (aliveEnemies == 0)
            {
                if (debugMode)
                    Debug.Log("[CombatUIController] ALL ENEMIES DEFEATED - VICTORY!");

                // Hide action menu
                if (actionMenuUI != null)
                {
                    actionMenuUI.Hide();
                }

                // End combat with victory
                if (_turnManager != null)
                {
                    _turnManager.EndCombat(CombatResult.Victory);
                }

                // TODO: Show Victory UI
                Debug.LogWarning("=== VICTORY ===");
            }
        }

        #endregion

        #region Action Handlers

        private void HandleAttackSelected()
        {
            if (debugMode)
                Debug.Log("[CombatUIController] Attack selected!");

            // Get player and target
            PlayerCombatUnit player = _battleManager?.PlayerCombat;
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCombatUnit>();
            }

            if (player == null)
            {
                Debug.LogError("[CombatUIController] No player found!");
                return;
            }

            if (!player.CanPerformAction(KAM.Core.Stats.ActionPointType.Action))
            {
                Debug.LogWarning("[CombatUIController] Not enough AP to attack.");
                if (CombatLogUI.Instance != null)
                    CombatLogUI.Instance.Log("Not enough AP to attack.");
                PopulateSkillsForPlayer();
                return;
            }

            // Get selected target from EnemyTargetSelector, or fallback to first enemy
            CombatUnit target = null;
            if (EnemyTargetSelector.Instance != null)
            {
                target = EnemyTargetSelector.Instance.GetTargetOrDefault();
            }

            // Fallback if no target selector
            if (target == null)
            {
                target = FindFirstEnemy();
            }

            if (target == null)
            {
                Debug.LogWarning("[CombatUIController] No enemy target found!");
                return;
            }

            _currentTarget = target;

            if (debugMode)
                Debug.Log($"[CombatUIController] Attacking selected target: {target.UnitName}");

            // Check if QTE should trigger
            bool triggerQTE = ShouldTriggerQTE();

            if (triggerQTE && _timedHitBridge != null)
            {
                if (debugMode)
                    Debug.Log("[CombatUIController] QTE TRIGGERED! Starting timed hit...");
                PerformTimedHitAttack(player, target);
            }
            else
            {
                if (debugMode && enableQTE)
                    Debug.Log("[CombatUIController] QTE did not trigger, performing basic attack");
                PerformBasicAttack(player, target);
            }
        }

        /// <summary>
        /// Determines if QTE should trigger based on settings and random chance
        /// </summary>
        private bool ShouldTriggerQTE()
        {
            // QTE disabled
            if (!enableQTE)
                return false;

            // Always trigger mode
            if (alwaysTriggerQTE)
                return true;

            // Roll random chance (0-100)
            float roll = Random.Range(0f, 100f);
            bool triggered = roll <= qteChance;

            if (debugMode)
                Debug.Log($"[CombatUIController] QTE Roll: {roll:F1}% (need <= {qteChance}%) = {(triggered ? "TRIGGERED!" : "no trigger")}");

            return triggered;
        }

        private void HandleDefendSelected()
        {
            if (debugMode)
                Debug.Log("[CombatUIController] Defend selected!");

            // Reset combo on defend
            KAM.Combat.ComboSystem.Defend();

            // Get player and execute defend (Free cost - doesn't consume AP)
            PlayerCombatUnit player = _battleManager?.PlayerCombat;
            if (player == null)
                player = FindFirstObjectByType<PlayerCombatUnit>();

            if (player != null)
            {
                var defendAction = DefendAction.CreateDefault();
                ActionResult result = defendAction.Execute(player, player);

                if (result.Success && debugMode)
                    Debug.Log($"[CombatUIController] {result.Message}");

                if (CombatLogUI.Instance != null)
                    CombatLogUI.Instance.Log(result.Message);
            }

            EndPlayerTurn();
        }

        private void HandleSkillSelected(CombatAction skill)
        {
            if (skill == null) return;

            if (debugMode)
                Debug.Log($"[CombatUIController] Skill selected: {skill.ActionName}");

            // Get player
            PlayerCombatUnit player = _battleManager?.PlayerCombat;
            if (player == null)
                player = FindFirstObjectByType<PlayerCombatUnit>();

            if (player == null)
            {
                Debug.LogError("[CombatUIController] No player found for skill!");
                return;
            }

            // For AoE skills (AllEnemies), no specific target needed
            CombatUnit target = null;
            if (skill.TargetingType == TargetType.SingleEnemy)
            {
                target = EnemyTargetSelector.Instance != null
                    ? EnemyTargetSelector.Instance.GetTargetOrDefault()
                    : FindFirstEnemy();

                if (target == null)
                {
                    Debug.LogWarning("[CombatUIController] No target for skill!");
                    return;
                }
            }

            // Execute the skill
            ActionResult result = skill.Execute(player, target);

            if (result.Success)
            {
                if (debugMode)
                    Debug.Log($"[CombatUIController] {skill.ActionName}: {result.Message}");

                // Spawn damage popups for each affected unit
                if (result.AffectedUnits != null)
                {
                    foreach (var affected in result.AffectedUnits)
                    {
                        if (affected != null && result.DamageDealt > 0)
                        {
                            int perTarget = result.AffectedUnits.Length > 0
                                ? result.DamageDealt / result.AffectedUnits.Length
                                : result.DamageDealt;
                            SpawnSkillDamagePopup(affected.transform.position, perTarget);
                        }
                    }
                }

                // Log to combat log
                if (CombatLogUI.Instance != null)
                {
                    CombatLogUI.Instance.Log(result.Message);
                }

                // Tick skill cooldowns
                if (_skillBridge != null)
                    _skillBridge.TickCooldowns();

                EndPlayerTurn();
            }
            else
            {
                Debug.LogWarning($"[CombatUIController] Skill failed: {result.Message}");

                // Log failure to combat log
                if (CombatLogUI.Instance != null)
                {
                    CombatLogUI.Instance.Log(result.Message);
                }
            }
        }

        /// <summary>
        /// Spawn damage popup for skill hits.
        /// </summary>
        private void SpawnSkillDamagePopup(Vector3 position, int damage)
        {
            if (damagePopupPrefab == null || damage <= 0) return;

            Vector3 spawnPos = position + Vector3.up * 2f;
            var popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);
            popup.ShowDamage(damage, false);
        }

        #endregion

        #region Enemy AI

        /// <summary>
        /// Execute enemy turn - simple AI that attacks the player
        /// </summary>
        private System.Collections.IEnumerator ExecuteEnemyTurn(CombatUnit enemy)
        {
            // Wait a moment before enemy acts (for visual feedback)
            yield return new WaitForSeconds(0.5f);

            // Find the player to attack
            CombatUnit player = _turnManager?.GetPlayer();
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCombatUnit>();
            }

            if (player == null || !player.IsAlive)
            {
                if (debugMode)
                    Debug.Log("[CombatUIController] No player target for enemy!");
                EndEnemyTurn(enemy);
                yield break;
            }

            if (enemy is EnemyCombatUnit enemyUnit)
            {
                EnemySkillController skillController = enemyUnit.GetComponent<EnemySkillController>();
                if (skillController != null && skillController.TryExecuteTurn(player, out EnemySkillExecution execution))
                {
                    PlayEnemyAttackVoice(enemy);

                    if (CombatLogUI.Instance != null && !string.IsNullOrEmpty(execution.Message))
                    {
                        CombatLogUI.Instance.Log(execution.Message);
                    }

                    if (execution.PopupTargets != null && execution.DamageResults != null)
                    {
                        int count = Mathf.Min(execution.PopupTargets.Count, execution.DamageResults.Count);
                        for (int i = 0; i < count; i++)
                        {
                            CombatUnit popupTarget = execution.PopupTargets[i];
                            if (popupTarget != null)
                            {
                                SpawnDamagePopup(popupTarget.transform.position, execution.DamageResults[i]);
                            }
                        }
                    }

                    yield return new WaitForSeconds(0.5f);
                    EndEnemyTurn(enemy);
                    yield break;
                }
            }

            // Enemy attacks player
            if (debugMode)
                Debug.Log($"[CombatUIController] {enemy.UnitName} attacks {player.UnitName}!");

            PlayEnemyAttackVoice(enemy);

            // Deal damage
            DamageResult result = enemy.DealDamage(player, enemy.Stats.EffectiveDamage);

            // Spawn damage popup on player
            SpawnDamagePopup(player.transform.position, result);

            if (!result.HitSuccessful && CombatLogUI.Instance != null)
            {
                CombatLogUI.Instance.Log($"{enemy.UnitName} missed!");
            }

            if (debugMode)
            {
                Debug.Log($"[CombatUIController] {enemy.UnitName} dealt {result.DamageDealt} damage to {player.UnitName}! " +
                         $"Player HP: {player.Stats.CurrentHealth}/{player.Stats.MaxHealth}");
            }

            // Wait for attack animation/feedback
            yield return new WaitForSeconds(0.5f);

            // End enemy turn
            EndEnemyTurn(enemy);
        }

        private void PlayEnemyAttackVoice(CombatUnit enemy)
        {
            if (enemy == null)
            {
                return;
            }

            EnemyVoiceController voice = enemy.GetComponent<EnemyVoiceController>();
            if (voice != null)
            {
                voice.PlayAttackVoice();
            }
        }

        private void EndEnemyTurn(CombatUnit enemy)
        {
            if (debugMode)
                Debug.Log($"[CombatUIController] {enemy.UnitName}'s turn ended");

            // Mark action performed for turn frequency system
            if (enemy is EnemyCombatUnit enemyUnit)
            {
                EnemySkillController skillController = enemyUnit.GetComponent<EnemySkillController>();
                if (skillController != null)
                {
                    skillController.NotifyActionFinished();
                }

                enemyUnit.OnActionPerformed();
            }

            // End turn in TurnManager
            if (_turnManager != null)
            {
                _turnManager.EndCurrentTurn();

                // Auto advance to next turn
                Invoke(nameof(AdvanceNextTurn), 0.3f);
            }
        }

        #endregion

        #region Attack Execution

        private void PerformBasicAttack(CombatUnit attacker, CombatUnit target)
        {
            if (debugMode)
                Debug.Log($"[CombatUIController] {attacker.UnitName} attacks {target.UnitName}!");

            // Consume 1 AP for basic attack
            if (attacker is PlayerCombatUnit playerAttacker)
            {
                if (!playerAttacker.ConsumeActionPoint(KAM.Core.Stats.ActionPointType.Action))
                {
                    Debug.LogWarning("[CombatUIController] Not enough AP for basic attack.");
                    if (CombatLogUI.Instance != null)
                        CombatLogUI.Instance.Log("Not enough AP to attack.");
                    PopulateSkillsForPlayer();
                    return;
                }
            }

            // Apply combo multiplier to damage
            float comboMultiplier = KAM.Combat.ComboSystem.GetMultiplier();
            int baseDmg = attacker.Stats.EffectiveDamage;
            int finalDmg = Mathf.RoundToInt(baseDmg * comboMultiplier);

            // Deal damage
            DamageResult result = attacker.DealDamage(target, finalDmg);

            // Register attack with combo system
            KAM.Combat.ComboSystem.Attack();

            // Spawn damage popup
            SpawnDamagePopup(target.transform.position, result);

            // Log result
            if (debugMode)
            {
                string comboInfo = comboMultiplier > 1f ? $" (Combo x{comboMultiplier:F1})" : "";
                Debug.Log($"[CombatUIController] Dealt {result.DamageDealt} damage{comboInfo}! " +
                         $"{target.UnitName} HP: {target.Stats.CurrentHealth}/{target.Stats.MaxHealth}");
            }

            // End turn after attack
            EndPlayerTurn();
        }

        private void PerformTimedHitAttack(PlayerCombatUnit player, CombatUnit target)
        {
            if (_timedHitBridge == null)
            {
                Debug.LogWarning("[CombatUIController] TimedHitBridge not found, using basic attack");
                PerformBasicAttack(player, target);
                return;
            }

            if (!player.CanPerformAction(KAM.Core.Stats.ActionPointType.Action))
            {
                Debug.LogWarning("[CombatUIController] Not enough AP for timed hit attack.");
                if (CombatLogUI.Instance != null)
                    CombatLogUI.Instance.Log("Not enough AP to attack.");
                PopulateSkillsForPlayer();
                return;
            }

            // AP is consumed by BasicAttackAction.Execute via OnBeforeExecute
            // Do NOT consume here to avoid double-consumption

            if (debugMode)
                Debug.Log("[CombatUIController] Starting timed hit attack...");

            _timedHitBridge.OnAttackCompleted -= HandleTimedHitAttackCompleted;
            _timedHitBridge.OnAttackCompleted += HandleTimedHitAttackCompleted;

            // Create a basic attack action
            var basicAttack = ScriptableObject.CreateInstance<KAM.Combat.Actions.BasicAttackAction>();

            // Start timed hit
            _timedHitBridge.StartQuickTimedHitAttack(basicAttack, player, target);
        }

        private void HandleTimedHitAttackCompleted(ActionResult result)
        {
            if (_timedHitBridge != null)
            {
                _timedHitBridge.OnAttackCompleted -= HandleTimedHitAttackCompleted;
            }

            if (damagePopupPrefab != null &&
                result.AffectedUnits != null &&
                result.AffectedUnits.Length > 0 &&
                result.AffectedUnits[0] != null)
            {
                if (result.DamageDealt > 0)
                {
                    var popup = Instantiate(damagePopupPrefab,
                        result.AffectedUnits[0].transform.position + Vector3.up * 2f,
                        Quaternion.identity);
                    popup.ShowDamage(result.DamageDealt, result.WasCritical);
                }
            }

            if (CombatLogUI.Instance != null)
            {
                CombatLogUI.Instance.Log(result.Message);
            }

            if (result.Success)
            {
                KAM.Combat.ComboSystem.Attack();
            }

            EndPlayerTurn();
        }

        private void HandleTimedHitComplete(TimedHitEventData data)
        {
            // Unsubscribe
            if (_timedHitController != null)
            {
                _timedHitController.OnTimedHitComplete -= HandleTimedHitComplete;
            }

            if (debugMode)
            {
                Debug.Log($"[CombatUIController] Timed hit complete! " +
                         $"Result: {data.Result}, Multiplier: {data.DamageMultiplier:F1}x");
            }

            // Actual popup/log/turn handling is done by HandleTimedHitAttackCompleted.
        }

        #endregion

        #region Helpers

        private CombatUnit FindFirstEnemy()
        {
            if (_turnManager != null)
            {
                var enemies = _turnManager.GetAliveEnemies();
                if (enemies.Count > 0)
                {
                    return enemies[0];
                }
            }

            // Fallback: find any EnemyCombatUnit
            var enemyUnits = FindObjectsByType<EnemyCombatUnit>(FindObjectsSortMode.None);
            foreach (var enemy in enemyUnits)
            {
                if (enemy.IsAlive)
                {
                    return enemy;
                }
            }

            return null;
        }

        private void SpawnDamagePopup(Vector3 position, DamageResult result)
        {
            if (damagePopupPrefab == null) return;

            Vector3 spawnPos = position + Vector3.up * 2f;
            var popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

            if (result.DamageInfo.IsCritical)
            {
                popup.ShowDamage(result.DamageDealt, true);
            }
            else
            {
                popup.ShowDamage(result.DamageDealt, false);
            }
        }

        private void EndPlayerTurn()
        {
            if (debugMode)
                Debug.Log("[CombatUIController] Ending player turn...");

            // Hide action menu
            if (actionMenuUI != null)
            {
                actionMenuUI.Hide();
            }

            // End turn in TurnManager
            if (_turnManager != null)
            {
                _turnManager.EndCurrentTurn();

                // Auto advance to next turn
                Invoke(nameof(AdvanceNextTurn), 0.5f);
            }
        }

        private void AdvanceNextTurn()
        {
            if (_turnManager != null && _turnManager.IsInCombat)
            {
                _turnManager.StartNextTurn();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the target for the next attack
        /// </summary>
        public void SetTarget(CombatUnit target)
        {
            _currentTarget = target;
        }

        /// <summary>
        /// Get current target
        /// </summary>
        public CombatUnit GetCurrentTarget()
        {
            return _currentTarget;
        }

        #endregion
    }

    /// <summary>
    /// Simple component to make a health bar follow a unit and update based on health changes.
    /// Used when creating health bars dynamically without a prefab.
    /// </summary>
    public class HealthBarFollower : MonoBehaviour
    {
        private CombatUnit _unit;
        private UnityEngine.UI.Image _fillImage;
        private TMPro.TextMeshProUGUI _healthText;
        private Vector3 _offset = new Vector3(0, 2.5f, 0);
        private Camera _mainCamera;

        public void Initialize(CombatUnit unit, UnityEngine.UI.Image fillImage, TMPro.TextMeshProUGUI healthText)
        {
            _unit = unit;
            _fillImage = fillImage;
            _healthText = healthText;
            _mainCamera = Camera.main;

            // Subscribe to health changes
            if (_unit != null && _unit.Stats != null)
            {
                _unit.Stats.OnHealthChanged += UpdateHealthDisplay;
                _unit.OnDeath += HandleDeath;
            }

            // Initial update
            UpdateHealthDisplay(_unit.Stats.CurrentHealth, _unit.Stats.MaxHealth);
        }

        private void OnDestroy()
        {
            if (_unit != null && _unit.Stats != null)
            {
                _unit.Stats.OnHealthChanged -= UpdateHealthDisplay;
                _unit.OnDeath -= HandleDeath;
            }
        }

        private void LateUpdate()
        {
            if (_unit == null)
            {
                Destroy(gameObject);
                return;
            }

            // Follow unit
            transform.position = _unit.transform.position + _offset;

            // Billboard to camera
            if (_mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }
        }

        private void UpdateHealthDisplay(int current, int max)
        {
            if (_fillImage != null)
            {
                float percentage = max > 0 ? (float)current / max : 0f;
                _fillImage.fillAmount = percentage;

                // Change color based on health
                if (percentage <= 0.25f)
                    _fillImage.color = new Color(0.8f, 0.2f, 0.2f); // Red
                else if (percentage <= 0.5f)
                    _fillImage.color = new Color(0.8f, 0.8f, 0.2f); // Yellow
                else
                    _fillImage.color = new Color(0.2f, 0.8f, 0.2f); // Green
            }

            if (_healthText != null)
            {
                _healthText.text = $"{current}/{max}";
            }
        }

        private void HandleDeath(CombatUnit unit)
        {
            // Fade out and destroy
            StartCoroutine(FadeOutAndDestroy());
        }

        private System.Collections.IEnumerator FadeOutAndDestroy()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                float duration = 0.5f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                    yield return null;
                }
            }

            Destroy(gameObject);
        }
    }
}
