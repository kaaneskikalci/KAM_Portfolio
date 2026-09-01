using UnityEngine;
using System;
using System.Collections.Generic;
using KAM.Combat.TurnSystem;
using KAM.Core.Combat;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Manages enemy target selection during combat.
    /// Allows player to click on enemies to select them as attack targets.
    /// Shows visual feedback (selection ring) on selected/hovered enemies.
    /// </summary>
    public class EnemyTargetSelector : MonoBehaviour
    {
        public static EnemyTargetSelector Instance { get; private set; }

        [Header("Selection Settings")]
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private float maxRaycastDistance = 100f;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject selectionRingPrefab;
        [SerializeField] private GameObject hoverRingPrefab;
        [SerializeField] private Color selectedColor = new Color(1f, 0.2f, 0.2f, 0.8f); // Red
        [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.2f, 0.6f); // Yellow

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Current selection state
        private EnemyCombatUnit _selectedTarget;
        private EnemyCombatUnit _hoveredTarget;
        private GameObject _selectionRingInstance;
        private GameObject _hoverRingInstance;

        // References
        private TurnManager _turnManager;
        private Camera _mainCamera;

        // Events
        public event Action<EnemyCombatUnit> OnTargetChanged;
        public event Action<EnemyCombatUnit> OnTargetHoverChanged;

        /// <summary>
        /// Currently selected enemy target. Returns null if none selected.
        /// </summary>
        public EnemyCombatUnit SelectedTarget => _selectedTarget;

        /// <summary>
        /// Currently hovered enemy (mouse over).
        /// </summary>
        public EnemyCombatUnit HoveredTarget => _hoveredTarget;

        /// <summary>
        /// Returns true if an enemy is currently selected.
        /// </summary>
        public bool HasTarget => _selectedTarget != null && _selectedTarget.IsAlive;

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Apply settings from CombatUIController before Start() runs.
        /// </summary>
        public void SetSettings(LayerMask layer, Color selectColor, Color hoverCol)
        {
            if (layer != 0)
                enemyLayer = layer;
            selectedColor = selectColor;
            hoverColor = hoverCol;
        }

        private void Start()
        {
            _turnManager = TurnManager.Instance;
            _mainCamera = Camera.main;

            // Auto-detect enemy layer if not set
            if (enemyLayer == 0)
            {
                AutoDetectEnemyLayer();
            }

            if (_turnManager != null)
            {
                _turnManager.OnCombatStart += OnCombatStart;
                _turnManager.OnCombatEnd += OnCombatEnd;
                _turnManager.OnTurnStart += OnTurnStart;
            }

            // Create selection ring instances
            CreateSelectionRings();

            if (debugMode)
                Debug.Log("[EnemyTargetSelector] Initialized");
        }

        /// <summary>
        /// Auto-detect enemy layer from existing EnemyCombatUnit GameObjects in the scene.
        /// Falls back to "Enemy" layer name, then to all layers.
        /// </summary>
        private void AutoDetectEnemyLayer()
        {
            // Try to find an enemy in the scene and use its layer
            var enemies = FindObjectsByType<EnemyCombatUnit>(FindObjectsSortMode.None);
            if (enemies.Length > 0)
            {
                int layer = enemies[0].gameObject.layer;
                enemyLayer = 1 << layer;
                if (debugMode)
                    Debug.Log($"[EnemyTargetSelector] Auto-detected enemy layer: {LayerMask.LayerToName(layer)} ({layer})");
                return;
            }

            // Try "Enemy" layer name
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            if (enemyLayerIndex >= 0)
            {
                enemyLayer = 1 << enemyLayerIndex;
                if (debugMode)
                    Debug.Log($"[EnemyTargetSelector] Using 'Enemy' layer ({enemyLayerIndex})");
                return;
            }

            // Fallback: use all layers (the fallback raycast in GetEnemyUnderMouse will handle it)
            enemyLayer = ~0;
            if (debugMode)
                Debug.Log("[EnemyTargetSelector] No enemy layer found, using all layers");
        }

        private void OnDestroy()
        {
            if (_turnManager != null)
            {
                _turnManager.OnCombatStart -= OnCombatStart;
                _turnManager.OnCombatEnd -= OnCombatEnd;
                _turnManager.OnTurnStart -= OnTurnStart;
            }

            // Cleanup ring instances
            if (_selectionRingInstance != null)
                Destroy(_selectionRingInstance);
            if (_hoverRingInstance != null)
                Destroy(_hoverRingInstance);

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // Only process input during combat and player turn
            if (_turnManager == null || !_turnManager.IsInCombat)
                return;

            // Allow selection anytime during player turn
            if (_turnManager.IsPlayerTurn)
            {
                HandleMouseHover();
                HandleMouseClick();
            }
            else
            {
                // Hide hover ring during enemy turn
                SetHoveredTarget(null);
            }

            // Update ring positions
            UpdateSelectionRingPosition();
            UpdateHoverRingPosition();
        }

        /// <summary>
        /// Handle mouse hover over enemies.
        /// </summary>
        private void HandleMouseHover()
        {
            EnemyCombatUnit hoveredEnemy = GetEnemyUnderMouse();
            SetHoveredTarget(hoveredEnemy);
        }

        /// <summary>
        /// Handle mouse click to select enemy.
        /// </summary>
        private void HandleMouseClick()
        {
            if (Input.GetMouseButtonDown(0))
            {
                EnemyCombatUnit clickedEnemy = GetEnemyUnderMouse();

                if (clickedEnemy != null)
                {
                    SelectTarget(clickedEnemy);
                }
            }
        }

        /// <summary>
        /// Get the enemy under the mouse cursor using raycast.
        /// </summary>
        private EnemyCombatUnit GetEnemyUnderMouse()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return null;
            }

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Try raycast with enemy layer
            if (Physics.Raycast(ray, out hit, maxRaycastDistance, enemyLayer))
            {
                // Check for EnemyCombatUnit on hit object or parents
                EnemyCombatUnit enemy = hit.collider.GetComponent<EnemyCombatUnit>();
                if (enemy == null)
                    enemy = hit.collider.GetComponentInParent<EnemyCombatUnit>();

                if (enemy != null && enemy.IsAlive)
                    return enemy;
            }

            // Fallback: raycast without layer filter
            if (Physics.Raycast(ray, out hit, maxRaycastDistance))
            {
                EnemyCombatUnit enemy = hit.collider.GetComponent<EnemyCombatUnit>();
                if (enemy == null)
                    enemy = hit.collider.GetComponentInParent<EnemyCombatUnit>();

                if (enemy != null && enemy.IsAlive)
                    return enemy;
            }

            return null;
        }

        /// <summary>
        /// Select an enemy as the attack target.
        /// </summary>
        public void SelectTarget(EnemyCombatUnit enemy)
        {
            if (enemy == null || !enemy.IsAlive)
            {
                ClearSelection();
                return;
            }

            // Don't reselect same target
            if (_selectedTarget == enemy)
                return;

            EnemyCombatUnit previousTarget = _selectedTarget;
            _selectedTarget = enemy;

            // Update visual feedback
            UpdateSelectionVisuals();

            // Fire event
            OnTargetChanged?.Invoke(_selectedTarget);

            if (debugMode)
                Debug.Log($"[EnemyTargetSelector] Selected target: {enemy.UnitName}");
        }

        /// <summary>
        /// Set the hovered target (for visual feedback only).
        /// </summary>
        private void SetHoveredTarget(EnemyCombatUnit enemy)
        {
            if (_hoveredTarget == enemy)
                return;

            _hoveredTarget = enemy;
            UpdateHoverVisuals();
            OnTargetHoverChanged?.Invoke(_hoveredTarget);
        }

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        public void ClearSelection()
        {
            if (_selectedTarget == null)
                return;

            _selectedTarget = null;
            UpdateSelectionVisuals();
            OnTargetChanged?.Invoke(null);

            if (debugMode)
                Debug.Log("[EnemyTargetSelector] Selection cleared");
        }

        /// <summary>
        /// Auto-select the first alive enemy.
        /// Called when combat starts or when selected enemy dies.
        /// </summary>
        public void AutoSelectFirstEnemy()
        {
            if (_turnManager == null)
                return;

            var enemies = _turnManager.GetAliveEnemies();
            if (enemies.Count > 0)
            {
                EnemyCombatUnit firstEnemy = enemies[0] as EnemyCombatUnit;
                if (firstEnemy != null)
                {
                    SelectTarget(firstEnemy);
                    if (debugMode)
                        Debug.Log($"[EnemyTargetSelector] Auto-selected first enemy: {firstEnemy.UnitName}");
                }
            }
            else
            {
                ClearSelection();
            }
        }

        /// <summary>
        /// Auto-select next enemy when current target dies.
        /// </summary>
        public void AutoSelectNextEnemy()
        {
            if (_turnManager == null)
                return;

            var enemies = _turnManager.GetAliveEnemies();

            // Find a different alive enemy
            foreach (var unit in enemies)
            {
                EnemyCombatUnit enemy = unit as EnemyCombatUnit;
                if (enemy != null && enemy != _selectedTarget && enemy.IsAlive)
                {
                    SelectTarget(enemy);
                    if (debugMode)
                        Debug.Log($"[EnemyTargetSelector] Auto-selected next enemy: {enemy.UnitName}");
                    return;
                }
            }

            // No other enemies alive
            ClearSelection();
        }

        #region Combat Events

        private void OnCombatStart()
        {
            // Auto-select first enemy when combat starts
            AutoSelectFirstEnemy();

            // Subscribe to death events for all enemies
            SubscribeToEnemyDeathEvents();

            if (debugMode)
                Debug.Log("[EnemyTargetSelector] Combat started - auto-selected first enemy");
        }

        private void OnCombatEnd()
        {
            ClearSelection();
            SetHoveredTarget(null);

            // Unsubscribe from death events
            UnsubscribeFromEnemyDeathEvents();

            if (debugMode)
                Debug.Log("[EnemyTargetSelector] Combat ended - cleared selection");
        }

        private void OnTurnStart(CombatUnit unit)
        {
            // Could update visuals based on whose turn it is
        }

        private void SubscribeToEnemyDeathEvents()
        {
            if (_turnManager == null) return;

            var enemies = _turnManager.GetAliveEnemies();
            foreach (var unit in enemies)
            {
                unit.OnDeath -= HandleEnemyDeath;
                unit.OnDeath += HandleEnemyDeath;
            }
        }

        private void UnsubscribeFromEnemyDeathEvents()
        {
            if (_turnManager == null) return;

            foreach (var unit in _turnManager.CombatUnits)
            {
                unit.OnDeath -= HandleEnemyDeath;
            }
        }

        private void HandleEnemyDeath(CombatUnit deadUnit)
        {
            // If the dead enemy was our selected target, auto-select next
            if (_selectedTarget != null && deadUnit == _selectedTarget)
            {
                if (debugMode)
                    Debug.Log($"[EnemyTargetSelector] Selected target died: {deadUnit.UnitName}");

                AutoSelectNextEnemy();
            }
        }

        #endregion

        #region Visual Feedback

        private void CreateSelectionRings()
        {
            // Create selection ring (red)
            if (selectionRingPrefab != null)
            {
                _selectionRingInstance = Instantiate(selectionRingPrefab);
                _selectionRingInstance.name = "SelectionRing";
                _selectionRingInstance.SetActive(false);
            }
            else
            {
                // Create a simple ring programmatically
                _selectionRingInstance = CreateSimpleRing("SelectionRing", selectedColor);
            }

            // Create hover ring (yellow)
            if (hoverRingPrefab != null)
            {
                _hoverRingInstance = Instantiate(hoverRingPrefab);
                _hoverRingInstance.name = "HoverRing";
                _hoverRingInstance.SetActive(false);
            }
            else
            {
                _hoverRingInstance = CreateSimpleRing("HoverRing", hoverColor);
            }
        }

        /// <summary>
        /// Create a simple ring using a cylinder primitive.
        /// </summary>
        private GameObject CreateSimpleRing(string name, Color color)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = name;

            // Remove collider
            Collider col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Configure transform - flat ring on ground
            ring.transform.localScale = new Vector3(2f, 0.02f, 2f);

            // Set material color - try URP first, then Standard
            Renderer renderer = ring.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = null;

                // Try URP Unlit shader first (works best for solid color rings)
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit != null)
                {
                    mat = new Material(urpUnlit);
                    mat.color = color;
                    mat.SetFloat("_Surface", 1); // 0=Opaque, 1=Transparent
                    mat.SetFloat("_Blend", 0);   // 0=Alpha
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0);
                    mat.SetFloat("_AlphaClip", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = 3000;
                }
                else
                {
                    // Try URP Lit
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null)
                    {
                        mat = new Material(urpLit);
                        mat.color = color;
                        mat.SetFloat("_Surface", 1);
                        mat.SetFloat("_Blend", 0);
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetFloat("_ZWrite", 0);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = 3000;
                    }
                    else
                    {
                        // Fallback to Standard shader (Built-in RP)
                        mat = new Material(Shader.Find("Standard"));
                        mat.color = color;
                        mat.SetFloat("_Mode", 3);
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = 3000;
                    }
                }

                renderer.material = mat;

                if (debugMode)
                    Debug.Log($"[EnemyTargetSelector] Created ring '{name}' with shader: {mat.shader.name}");
            }

            ring.SetActive(false);
            return ring;
        }

        private void UpdateSelectionVisuals()
        {
            if (_selectionRingInstance == null)
                return;

            _selectionRingInstance.SetActive(_selectedTarget != null && _selectedTarget.IsAlive);
        }

        private void UpdateHoverVisuals()
        {
            if (_hoverRingInstance == null)
                return;

            // Don't show hover ring if same as selected target
            bool showHover = _hoveredTarget != null &&
                            _hoveredTarget.IsAlive &&
                            _hoveredTarget != _selectedTarget;

            _hoverRingInstance.SetActive(showHover);
        }

        private void UpdateSelectionRingPosition()
        {
            if (_selectionRingInstance == null || !_selectionRingInstance.activeSelf)
                return;

            if (_selectedTarget != null)
            {
                // Position ring under the enemy, on the ground
                Vector3 targetPos = _selectedTarget.transform.position;
                targetPos.y = 0.05f; // Slightly above ground to avoid z-fighting
                _selectionRingInstance.transform.position = targetPos;
            }
        }

        private void UpdateHoverRingPosition()
        {
            if (_hoverRingInstance == null || !_hoverRingInstance.activeSelf)
                return;

            if (_hoveredTarget != null)
            {
                Vector3 targetPos = _hoveredTarget.transform.position;
                targetPos.y = 0.05f;
                _hoverRingInstance.transform.position = targetPos;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the currently selected target, or first enemy as fallback.
        /// Use this in attack logic.
        /// </summary>
        public CombatUnit GetTargetOrDefault()
        {
            if (_selectedTarget != null && _selectedTarget.IsAlive)
                return _selectedTarget;

            // Fallback to first alive enemy
            if (_turnManager != null)
            {
                var enemies = _turnManager.GetAliveEnemies();
                if (enemies.Count > 0)
                    return enemies[0];
            }

            return null;
        }

        /// <summary>
        /// Check if a specific enemy is currently selected.
        /// </summary>
        public bool IsSelected(EnemyCombatUnit enemy)
        {
            return _selectedTarget == enemy;
        }

        /// <summary>
        /// Check if a specific enemy is currently hovered.
        /// </summary>
        public bool IsHovered(EnemyCombatUnit enemy)
        {
            return _hoveredTarget == enemy;
        }

        #endregion
    }
}
