using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using KAM.Combat.TurnSystem;
using KAM.SaveSystem;
using KAM.UI.Feedback;

namespace KAM.Core
{
    /// <summary>
    /// Top-level game state machine. Orchestrates transitions between Oba (hub) and Spirit Realm (run).
    /// Singleton, DontDestroyOnLoad.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance => _instance;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "Start";
        [SerializeField] private string obaSceneName = "OBA";
        [SerializeField] private string spiritRealmSceneName = "Lvl1";

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        private GameState _currentState = GameState.Initializing;

        // Properties
        public GameState CurrentState => _currentState;
        public bool IsInHub => _currentState == GameState.InHub;
        public bool IsInRun => _currentState == GameState.InRun;

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action OnRunStarted;
        public event Action<RunEndReason> OnRunEnded;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            NormalizeSceneNames();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                _instance = null;
            }
        }

        private void Start()
        {
            SyncStateWithActiveScene();
        }

        #region Run Lifecycle

        /// <summary>
        /// Start a new run. Called by SpiritRealmPortal interaction.
        /// </summary>
        public void StartRun()
        {
            SyncStateWithActiveScene();

            if (_currentState != GameState.InHub)
            {
                Debug.LogWarning($"GameManager: Can only start a run from the hub! Current scene: {SceneManager.GetActiveScene().name}, state: {_currentState}");
                return;
            }

            SetState(GameState.StartingRun);

            // Clear temporary items (keep spirit fragments)
            if (KAM.Inventory.InventoryManager.Instance != null)
            {
                KAM.Inventory.InventoryManager.Instance.ClearTemporaryItems();
            }

            if (showDebugInfo)
            {
                Debug.Log("GameManager: Starting run, loading Spirit Realm...");
            }

            SceneTransitionManager.LoadSceneWithFade(spiritRealmSceneName);
        }

        /// <summary>
        /// End the current run. Called on player death or victory.
        /// </summary>
        public void EndRun(RunEndReason reason)
        {
            if (_currentState != GameState.InRun)
            {
                return;
            }

            SetState(GameState.RunEnded);
            OnRunEnded?.Invoke(reason);

            if (showDebugInfo)
            {
                Debug.Log($"GameManager: Run ended - {reason}");
            }

            ReturnToHub();
        }

        /// <summary>
        /// Load the Oba hub scene and reset run state.
        /// </summary>
        public void ReturnToHub()
        {
            SetState(GameState.ReturningToHub);

            // Clear temporary items (keep spirit fragments and skill unlocks)
            if (KAM.Inventory.InventoryManager.Instance != null)
            {
                KAM.Inventory.InventoryManager.Instance.ClearTemporaryItems();
            }

            if (showDebugInfo)
            {
                Debug.Log("GameManager: Returning to Oba...");
            }

            SceneTransitionManager.LoadSceneWithFade(obaSceneName);
        }

        #endregion

        #region Scene Events

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsScene(scene.name, mainMenuSceneName))
            {
                SetState(GameState.Initializing);
            }
            else if (IsScene(scene.name, obaSceneName))
            {
                SetState(GameState.InHub);

                if (showDebugInfo)
                {
                    Debug.Log("GameManager: Arrived at Oba hub.");
                }
            }
            else if (IsScene(scene.name, spiritRealmSceneName))
            {
                SetState(GameState.InRun);
                OnRunStarted?.Invoke();

                // Subscribe to combat events for defeat detection
                SubscribeToCombatEvents();

                if (showDebugInfo)
                {
                    Debug.Log("GameManager: Entered Spirit Realm.");
                }
            }
        }

        /// <summary>
        /// Subscribe to TurnManager's combat end event to detect defeat.
        /// Must be called after scene load since TurnManager is scene-bound.
        /// </summary>
        private void SubscribeToCombatEvents()
        {
            // Use a delayed call to ensure TurnManager has been initialized
            Invoke(nameof(TrySubscribeToCombatEvents), 0.1f);
        }

        private void TrySubscribeToCombatEvents()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnCombatEndWithResult += HandleCombatEndWithResult;
            }
        }

        private void HandleCombatEndWithResult(CombatResult result)
        {
            if (result == CombatResult.Defeat)
            {
                if (showDebugInfo)
                {
                    Debug.Log("GameManager: Player defeated! Showing death UI...");
                }

                DeathUI.ShowRespawnOrFallback(() => EndRun(RunEndReason.Defeat));
            }
        }

        #endregion

        #region State Management

        private void SyncStateWithActiveScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            if (IsScene(currentScene, mainMenuSceneName))
            {
                SetState(GameState.Initializing);
                return;
            }

            if (IsScene(currentScene, obaSceneName))
            {
                SetState(GameState.InHub);
                return;
            }

            if (IsScene(currentScene, spiritRealmSceneName))
            {
                SetState(GameState.InRun);
            }
        }

        private void NormalizeSceneNames()
        {
            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
                mainMenuSceneName = "Start";

            if (string.IsNullOrWhiteSpace(obaSceneName))
                obaSceneName = "OBA";

            if (string.IsNullOrWhiteSpace(spiritRealmSceneName) || spiritRealmSceneName == "KaanMovementAndCamera1")
                spiritRealmSceneName = "Lvl1";
        }

        private static bool IsScene(string sceneName, string expectedName)
        {
            return string.Equals(sceneName, expectedName, StringComparison.OrdinalIgnoreCase);
        }

        private void SetState(GameState newState)
        {
            if (_currentState == newState) return;

            _currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }

        #endregion
    }

    public enum GameState
    {
        Initializing,
        InHub,
        StartingRun,
        InRun,
        RunEnded,
        ReturningToHub
    }

    public enum RunEndReason
    {
        Defeat,
        Victory,
        Fled
    }
}
