using UnityEngine;
using System.Collections;
using KAM.Core.Combat;
using KAM.Combat.TurnSystem;
using KAM.Combat.Actions;
using KAM.Combat.TimedHit;

namespace KAM.Combat
{
    /// <summary>
    /// Test script to verify combat system is working.
    /// Add to any GameObject in scene and press keys to test combat.
    /// </summary>
    public class CombatTestRunner : MonoBehaviour
    {
        [Header("References (Auto-found if empty)")]
        [SerializeField] private PlayerCombatUnit player;
        [SerializeField] private EnemyCombatUnit[] enemies;

        [Header("Test Actions")]
        [SerializeField] private BasicAttackAction testAttack;

        [Header("Test Settings")]
        [SerializeField] private bool autoStartCombat = false;
        [SerializeField] private float autoStartDelay = 2f;

        [Header("Debug Keys")]
        [SerializeField] private KeyCode startCombatKey = KeyCode.F1;
        [SerializeField] private KeyCode attackKey = KeyCode.F2;
        [SerializeField] private KeyCode endTurnKey = KeyCode.F3;
        [SerializeField] private KeyCode endCombatKey = KeyCode.F4;
        [SerializeField] private KeyCode timedHitAttackKey = KeyCode.F5;

        [Header("Timed Hit (Phase 3)")]
        [SerializeField] private TimedHitConfig timedHitConfig;
        [SerializeField] private bool enableTimedHitTesting = true;

        private TurnManager _turnManager;
        private BattleManager _battleManager;
        private bool _initialized = false;

        // Timed Hit components (created at runtime for testing)
        private TimedHitController _timedHitController;
        private TimedHitInput _timedHitInput;
        private TimedHitCombatBridge _timedHitBridge;
        private bool _isTimedHitAttackInProgress;

        private void Start()
        {
            StartCoroutine(InitializeDelayed());
        }

        private IEnumerator InitializeDelayed()
        {
            // Wait a frame for other components to initialize
            yield return null;

            Initialize();

            if (autoStartCombat)
            {
                yield return new WaitForSeconds(autoStartDelay);
                StartTestCombat();
            }
        }

        private void Initialize()
        {
            // Find references if not assigned
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCombatUnit>();
            }

            if (enemies == null || enemies.Length == 0)
            {
                enemies = FindObjectsByType<EnemyCombatUnit>(FindObjectsSortMode.None);
            }

            _turnManager = TurnManager.Instance;
            _battleManager = BattleManager.Instance;

            // Create test attack if not assigned
            if (testAttack == null)
            {
                testAttack = BasicAttackAction.CreateDefault();
            }

            // Subscribe to events for logging
            if (_turnManager != null)
            {
                _turnManager.OnCombatStart += () => Debug.Log("<color=green>[TEST] Combat Started!</color>");
                _turnManager.OnCombatEnd += () => Debug.Log("<color=red>[TEST] Combat Ended!</color>");
                _turnManager.OnTurnStart += (unit) => Debug.Log($"<color=yellow>[TEST] {unit.UnitName}'s turn started</color>");
                _turnManager.OnRoundStart += (round) => Debug.Log($"<color=cyan>[TEST] Round {round} started</color>");
            }

            // Setup timed hit components for testing
            if (enableTimedHitTesting)
            {
                SetupTimedHitComponents();
            }

            _initialized = true;

            Debug.Log($"<color=white>[CombatTestRunner] Initialized</color>\n" +
                     $"  Player: {(player != null ? player.UnitName : "NOT FOUND")}\n" +
                     $"  Enemies: {enemies?.Length ?? 0}\n" +
                     $"  TurnManager: {(_turnManager != null ? "OK" : "NOT FOUND")}\n" +
                     $"  BattleManager: {(_battleManager != null ? "OK" : "NOT FOUND")}\n" +
                     $"  TimedHit: {(_timedHitController != null ? "OK" : "DISABLED")}\n\n" +
                     $"  Controls:\n" +
                     $"  {startCombatKey} - Start Combat\n" +
                     $"  {attackKey} - Attack (during player turn)\n" +
                     $"  {endTurnKey} - End Turn\n" +
                     $"  {endCombatKey} - Force End Combat\n" +
                     $"  {timedHitAttackKey} - Timed Hit Attack (Arrow Keys to hit)");
        }

        private void SetupTimedHitComponents()
        {
            // Create a child GameObject for timed hit system
            GameObject timedHitObj = new GameObject("TimedHitSystem");
            timedHitObj.transform.SetParent(transform);

            // Add controller
            _timedHitController = timedHitObj.AddComponent<TimedHitController>();
            if (timedHitConfig != null)
            {
                _timedHitController.SetConfig(timedHitConfig);
            }

            // Add input handler
            _timedHitInput = timedHitObj.AddComponent<TimedHitInput>();
            _timedHitInput.SetController(_timedHitController);

            // Add combat bridge
            _timedHitBridge = timedHitObj.AddComponent<TimedHitCombatBridge>();
            _timedHitBridge.SetReferences(_timedHitController, _timedHitInput);

            // Subscribe to events
            _timedHitController.OnSequenceStarted += OnTimedHitSequenceStarted;
            _timedHitController.OnWindowOpened += OnTimedHitWindowOpened;
            _timedHitController.OnTimedHitComplete += OnTimedHitComplete;
            _timedHitBridge.OnAttackCompleted += OnTimedHitAttackCompleted;

            Debug.Log("<color=magenta>[TEST] Timed Hit System initialized</color>");
        }

        private void OnTimedHitSequenceStarted(RuneDirection direction)
        {
            Debug.Log($"<color=magenta>[TEST] Timed Hit Started - Press {direction}!</color>");
        }

        private void OnTimedHitWindowOpened()
        {
            Debug.Log("<color=magenta>[TEST] Timed Hit Window OPEN - Press the direction NOW!</color>");
        }

        private void OnTimedHitComplete(TimedHitEventData eventData)
        {
            string resultColor = eventData.Result == TimedHitResult.Perfect ? "cyan" :
                                eventData.Result == TimedHitResult.Success ? "green" : "red";
            Debug.Log($"<color={resultColor}>[TEST] Timed Hit Result: {eventData.Result} (x{eventData.DamageMultiplier:F1})</color>");
        }

        private void OnTimedHitAttackCompleted(ActionResult result)
        {
            _isTimedHitAttackInProgress = false;
            Debug.Log($"<color=cyan>[TEST] Timed Hit Attack Complete: {result.Message}</color>");
        }

        private void Update()
        {
            if (!_initialized) return;

            // Start Combat
            if (Input.GetKeyDown(startCombatKey))
            {
                StartTestCombat();
            }

            // Attack
            if (Input.GetKeyDown(attackKey))
            {
                PerformTestAttack();
            }

            // End Turn
            if (Input.GetKeyDown(endTurnKey))
            {
                EndCurrentTurn();
            }

            // Force End Combat
            if (Input.GetKeyDown(endCombatKey))
            {
                ForceEndCombat();
            }

            // Timed Hit Attack
            if (Input.GetKeyDown(timedHitAttackKey))
            {
                PerformTimedHitAttack();
            }
        }

        /// <summary>
        /// Perform an attack with timed hit minigame.
        /// </summary>
        public void PerformTimedHitAttack()
        {
            if (_timedHitBridge == null || _timedHitController == null)
            {
                Debug.LogWarning("[TEST] Timed Hit system not initialized!");
                return;
            }

            if (_isTimedHitAttackInProgress)
            {
                Debug.LogWarning("[TEST] Timed Hit attack already in progress!");
                return;
            }

            if (_turnManager == null || !_turnManager.IsInCombat)
            {
                Debug.LogWarning("[TEST] Not in combat! Press F1 to start combat first.");
                return;
            }

            if (!_turnManager.IsPlayerTurn)
            {
                Debug.LogWarning("[TEST] Not player's turn!");
                return;
            }

            // Find first alive enemy
            CombatUnit target = null;
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    target = enemy;
                    break;
                }
            }

            if (target == null)
            {
                Debug.LogWarning("[TEST] No valid targets!");
                return;
            }

            Debug.Log($"<color=magenta>[TEST] Starting Timed Hit Attack on {target.UnitName}...</color>");
            Debug.Log($"<color=magenta>[TEST] Watch for the direction and press Arrow Keys when the window opens!</color>");

            _isTimedHitAttackInProgress = true;
            _timedHitBridge.StartQuickTimedHitAttack(testAttack, player, target);
        }

        /// <summary>
        /// Start a test combat encounter.
        /// </summary>
        public void StartTestCombat()
        {
            if (_turnManager == null)
            {
                Debug.LogError("[TEST] TurnManager not found!");
                return;
            }

            if (_turnManager.IsInCombat)
            {
                Debug.LogWarning("[TEST] Already in combat!");
                return;
            }

            if (player == null)
            {
                Debug.LogError("[TEST] Player not found!");
                return;
            }

            if (enemies == null || enemies.Length == 0)
            {
                Debug.LogError("[TEST] No enemies found!");
                return;
            }

            Debug.Log("<color=green>[TEST] Starting combat...</color>");

            // Build unit list
            var combatUnits = new System.Collections.Generic.List<CombatUnit>();
            combatUnits.Add(player);

            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    combatUnits.Add(enemy);
                }
            }

            // Start combat through TurnManager directly
            _turnManager.StartCombat(combatUnits);
        }

        /// <summary>
        /// Perform a test attack on the first alive enemy.
        /// </summary>
        public void PerformTestAttack()
        {
            if (_turnManager == null || !_turnManager.IsInCombat)
            {
                Debug.LogWarning("[TEST] Not in combat!");
                return;
            }

            if (!_turnManager.IsPlayerTurn)
            {
                Debug.LogWarning("[TEST] Not player's turn!");
                return;
            }

            // Find first alive enemy
            CombatUnit target = null;
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    target = enemy;
                    break;
                }
            }

            if (target == null)
            {
                Debug.LogWarning("[TEST] No valid targets!");
                return;
            }

            Debug.Log($"<color=cyan>[TEST] Attacking {target.UnitName}...</color>");

            // Execute attack
            if (testAttack != null && testAttack.CanExecute(player, target))
            {
                var result = testAttack.Execute(player, target);
                Debug.Log($"<color=cyan>[TEST] {result.Message}</color>");

                // Log target health
                Debug.Log($"<color=cyan>[TEST] {target.UnitName} HP: {target.Stats.CurrentHealth}/{target.Stats.MaxHealth}</color>");
            }
            else
            {
                // Manual damage if action can't execute
                var damageResult = player.DealDamage(target, 10);
                Debug.Log($"<color=cyan>[TEST] Dealt {damageResult.DamageDealt} damage to {target.UnitName}</color>");
                Debug.Log($"<color=cyan>[TEST] {target.UnitName} HP: {target.Stats.CurrentHealth}/{target.Stats.MaxHealth}</color>");
            }
        }

        /// <summary>
        /// End the current turn.
        /// </summary>
        public void EndCurrentTurn()
        {
            if (_turnManager == null || !_turnManager.IsInCombat)
            {
                Debug.LogWarning("[TEST] Not in combat!");
                return;
            }

            Debug.Log("<color=yellow>[TEST] Ending turn...</color>");
            _turnManager.EndCurrentTurn();

            // Auto advance to next turn
            StartCoroutine(AdvanceAfterDelay(0.5f));
        }

        private IEnumerator AdvanceAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (_turnManager != null && _turnManager.IsInCombat)
            {
                _turnManager.StartNextTurn();
            }
        }

        /// <summary>
        /// Force end combat.
        /// </summary>
        public void ForceEndCombat()
        {
            if (_turnManager != null && _turnManager.IsInCombat)
            {
                Debug.Log("<color=red>[TEST] Force ending combat...</color>");
                _turnManager.EndCombat(CombatResult.None);
            }
            else if (_battleManager != null && _battleManager.InCombat)
            {
                Debug.Log("<color=red>[TEST] Force ending combat via BattleManager...</color>");
                _battleManager.EndCombat();
            }
            else
            {
                Debug.LogWarning("[TEST] Not in combat!");
            }
        }

        private void OnGUI()
        {
            if (!_initialized) return;

            // Draw combat status in corner
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));

            GUILayout.Label("<b>Combat Test Runner</b>", new GUIStyle { richText = true, fontSize = 16, normal = { textColor = Color.white } });

            string combatStatus = _turnManager != null && _turnManager.IsInCombat ? "IN COMBAT" : "Not in combat";
            Color statusColor = _turnManager != null && _turnManager.IsInCombat ? Color.green : Color.gray;
            GUILayout.Label($"Status: <color=#{ColorUtility.ToHtmlStringRGB(statusColor)}>{combatStatus}</color>",
                new GUIStyle { richText = true, normal = { textColor = Color.white } });

            if (_turnManager != null && _turnManager.IsInCombat)
            {
                GUILayout.Label($"Round: {_turnManager.CurrentRound}");
                GUILayout.Label($"Current Turn: {_turnManager.CurrentUnit?.UnitName ?? "None"}");
                GUILayout.Label($"Is Player Turn: {_turnManager.IsPlayerTurn}");

                GUILayout.Space(10);
                GUILayout.Label("Units:");

                if (player != null)
                {
                    string playerHP = player.IsAlive ?
                        $"{player.Stats.CurrentHealth}/{player.Stats.MaxHealth}" : "DEAD";
                    GUILayout.Label($"  [P] {player.UnitName}: {playerHP}");
                }

                foreach (var enemy in enemies)
                {
                    if (enemy != null)
                    {
                        string enemyHP = enemy.IsAlive ?
                            $"{enemy.Stats.CurrentHealth}/{enemy.Stats.MaxHealth}" : "DEAD";
                        GUILayout.Label($"  [E] {enemy.UnitName}: {enemyHP}");
                    }
                }
            }

            GUILayout.Space(10);
            GUILayout.Label($"Controls:");
            GUILayout.Label($"  {startCombatKey} - Start Combat");
            GUILayout.Label($"  {attackKey} - Attack");
            GUILayout.Label($"  {endTurnKey} - End Turn");
            GUILayout.Label($"  {endCombatKey} - End Combat");
            GUILayout.Label($"  {timedHitAttackKey} - Timed Hit Attack");

            // Show Timed Hit status
            if (_timedHitController != null && _timedHitController.IsActive)
            {
                GUILayout.Space(10);
                GUILayout.Label("<b>TIMED HIT ACTIVE!</b>", new GUIStyle { richText = true, fontSize = 18, normal = { textColor = Color.magenta } });
                GUILayout.Label($"Direction: {_timedHitController.CurrentDirection}");
                GUILayout.Label($"State: {_timedHitController.CurrentState}");
                if (_timedHitController.CurrentState == TimedHitState.WindowActive)
                {
                    string key = _timedHitController.CurrentDirection switch
                    {
                        RuneDirection.Up => "↑",
                        RuneDirection.Down => "↓",
                        RuneDirection.Left => "←",
                        RuneDirection.Right => "→",
                        _ => "?"
                    };
                    GUILayout.Label($"Press: {key}", new GUIStyle { fontSize = 24, normal = { textColor = Color.yellow } });
                    GUILayout.Label($"Time: {_timedHitController.RemainingTime:F2}s");
                }
            }

            GUILayout.EndArea();
        }
    }
}
