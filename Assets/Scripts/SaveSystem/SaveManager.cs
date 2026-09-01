using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KAM.Core;
using KAM.Core.Combat;
using KAM.Inventory;
using KAM.Progression;
using KAM.UI.Feedback;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KAM.SaveSystem
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Save Settings")]
        [SerializeField] private string saveFileName = "kam_save.json";
        [SerializeField] private bool autoLoadOnStart = false;

        [Header("Death Penalty")]
        [SerializeField, Range(0f, 1f)] private float deathSpiritFragmentLossPercent = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;
        [SerializeField] private bool showRuntimeDebugOverlay = false;
        [SerializeField] private KeyCode toggleDebugOverlayKey = KeyCode.F9;

        private GameSaveData _loadedSave;
        private BonfireSaveData _pendingBonfireSpawn;
        private Coroutine _pendingBonfireSpawnRoutine;
        private bool _isRespawningFromDeath;

        public string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
        public GameSaveData LoadedSave => _loadedSave;
        public bool HasLoadedSave => _loadedSave != null;
        public bool IsRespawningFromDeath => _isRespawningFromDeath;

        private void OnValidate()
        {
            deathSpiritFragmentLossPercent = Mathf.Clamp01(deathSpiritFragmentLossPercent);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            if (autoLoadOnStart && HasSaveFile())
            {
                LoadGame(loadSavedScene: false);
            }
        }

        private void Update()
        {
            if (toggleDebugOverlayKey != KeyCode.None && Input.GetKeyDown(toggleDebugOverlayKey))
                showRuntimeDebugOverlay = !showRuntimeDebugOverlay;
        }

        private void OnGUI()
        {
            if (!showRuntimeDebugOverlay)
                return;

            const int width = 520;
            GUILayout.BeginArea(new Rect(16, 16, width, Screen.height - 32), GUI.skin.box);
            GUILayout.Label(GetDebugSummary());

            if (GUILayout.Button("Force Respawn To Saved Bonfire"))
                ForceRespawnAtSavedBonfireForDebug();

            if (GUILayout.Button("Print Save State To Console"))
                DebugPrintSaveState();

            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                Instance = null;
            }
        }

        public static SaveManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            GameObject saveManagerObject = new GameObject("SaveManager");
            return saveManagerObject.AddComponent<SaveManager>();
        }

        public bool HasSaveFile()
        {
            return File.Exists(SavePath);
        }

        public void RecordBonfire(BonfireSavePoint bonfire)
        {
            if (bonfire == null)
                return;

            if (_loadedSave == null)
                _loadedSave = CaptureCurrentState();

            _loadedSave.bonfire = bonfire.ToSaveData();
            _loadedSave.discoveredBonfires = UpsertDiscoveredBonfire(_loadedSave.discoveredBonfires, _loadedSave.bonfire);
            _loadedSave.activeSceneName = _loadedSave.bonfire.sceneName;

            if (debugLog)
            {
                Debug.Log($"[SaveManager] Recorded bonfire '{_loadedSave.bonfire.bonfireId}' in scene '{_loadedSave.bonfire.sceneName}' at {_loadedSave.bonfire.position.ToVector3()}.");
            }
        }

        public bool SaveGame()
        {
            GameSaveData data = CaptureCurrentState();

            if (_loadedSave != null && _loadedSave.bonfire != null)
                data.bonfire = _loadedSave.bonfire;

            data.discoveredBonfires = _loadedSave?.discoveredBonfires;
            if (data.bonfire != null)
                data.discoveredBonfires = UpsertDiscoveredBonfire(data.discoveredBonfires, data.bonfire);

            data.savedAtUtc = DateTime.UtcNow.ToString("O");
            _loadedSave = data;

            return WriteSaveData(data);
        }

        public bool LoadGame(bool loadSavedScene = true)
        {
            GameSaveData data = ReadSaveData();
            if (data == null)
                return false;

            try
            {
                _loadedSave = data;
                ApplyPersistentState(data);
                ApplySceneState(GetSceneSaveData(data, SceneManager.GetActiveScene().name));

                if (loadSavedScene && !string.IsNullOrWhiteSpace(data.activeSceneName))
                {
                    if (data.HasBonfire)
                        _pendingBonfireSpawn = data.bonfire;

                    if (SceneManager.GetActiveScene().name != data.activeSceneName)
                    {
                        EditorSelectionGuard.ClearSelection();
                        if (data.HasBonfire)
                            SceneTransitionManager.BeginBlackHold();

                        SceneTransitionManager.LoadSceneWithFade(data.activeSceneName);
                    }
                    else
                    {
                        if (data.HasBonfire)
                            SceneTransitionManager.BeginBlackHold();

                        QueuePendingBonfireSpawn();
                    }
                }

                if (debugLog)
                    Debug.Log($"[SaveManager] Loaded game from {SavePath}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
                return false;
            }
        }

        public bool RestAtBonfire(BonfireSavePoint bonfire)
        {
            if (bonfire == null)
                return false;

            RecordBonfire(bonfire);
            RestorePlayerToFullHealth();

            if (!SaveGame())
                return false;

            _pendingBonfireSpawn = _loadedSave?.bonfire;
            RunFeedbackUI.EnsureInstance().ShowBonfireRest(bonfire.BonfireId);
            EditorSelectionGuard.ClearSelection();
            SceneTransitionManager.BeginBlackHold();
            SceneTransitionManager.LoadSceneWithFade(bonfire.SceneName);
            return true;
        }

        public bool RespawnAtLastBonfireAfterDeath()
        {
            GameSaveData data = _loadedSave ?? ReadSaveData();
            if (data == null || !data.HasBonfire)
            {
                Debug.LogWarning("[SaveManager] Cannot respawn: no bonfire save found.");
                return false;
            }

            if (_isRespawningFromDeath)
            {
                _pendingBonfireSpawn = data.bonfire;
                LoadOrQueueBonfireScene(data.bonfire, "Death respawn already in progress");
                return true;
            }

            _isRespawningFromDeath = true;
            int lostFragments = ApplyDeathPenalty(data);
            data.activeSceneName = data.bonfire.sceneName;
            data.savedAtUtc = DateTime.UtcNow.ToString("O");
            _loadedSave = data;

            WriteSaveData(data);

            _pendingBonfireSpawn = data.bonfire;
            RunFeedbackUI.EnsureInstance().ShowDefeatedRespawn(lostFragments, data.bonfire.bonfireId);
            EditorSelectionGuard.ClearSelection();

            if (debugLog)
            {
                Vector3 spawnPosition = data.bonfire.position.ToVector3();
                Debug.Log($"[SaveManager] Death respawn queued at bonfire '{data.bonfire.bonfireId}' in scene '{data.bonfire.sceneName}' position {spawnPosition}.");
            }

            LoadOrQueueBonfireScene(data.bonfire, "Death respawn");
            return true;
        }

        private void LoadOrQueueBonfireScene(BonfireSaveData bonfire, string reason)
        {
            if (bonfire == null || string.IsNullOrWhiteSpace(bonfire.sceneName))
            {
                Debug.LogWarning($"[SaveManager] Cannot load bonfire scene for {reason}: missing scene name.");
                return;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            _pendingBonfireSpawn = bonfire;
            EditorSelectionGuard.ClearSelection();

            if (currentScene == bonfire.sceneName)
            {
                if (debugLog)
                    Debug.Log($"[SaveManager] {reason}: already in scene '{bonfire.sceneName}', queueing spawn.");

                SceneTransitionManager.BeginBlackHold();
                QueuePendingBonfireSpawn();
                return;
            }

            if (debugLog)
                Debug.Log($"[SaveManager] {reason}: loading scene '{bonfire.sceneName}' from '{currentScene}'.");

            SceneTransitionManager.BeginBlackHold();
            SceneTransitionManager.LoadSceneWithFade(bonfire.sceneName);
        }

        public IReadOnlyList<BonfireSaveData> GetDiscoveredBonfires()
        {
            if (_loadedSave?.discoveredBonfires == null)
                return Array.Empty<BonfireSaveData>();

            return _loadedSave.discoveredBonfires;
        }

        public bool TravelToBonfire(string bonfireId)
        {
            if (string.IsNullOrWhiteSpace(bonfireId))
                return false;

            if (_loadedSave == null)
                _loadedSave = ReadSaveData() ?? CaptureCurrentState();

            BonfireSaveData target = FindDiscoveredBonfire(_loadedSave.discoveredBonfires, bonfireId);
            if (target == null)
            {
                Debug.LogWarning($"[SaveManager] Cannot travel: bonfire '{bonfireId}' is not discovered.");
                return false;
            }

            GameSaveData data = CaptureCurrentState();
            data.bonfire = target;
            data.discoveredBonfires = _loadedSave.discoveredBonfires;
            data.activeSceneName = target.sceneName;
            data.savedAtUtc = DateTime.UtcNow.ToString("O");
            _loadedSave = data;

            WriteSaveData(data);

            _pendingBonfireSpawn = target;
            RunFeedbackUI.EnsureInstance().ShowBonfireTravel(target.bonfireId);
            EditorSelectionGuard.ClearSelection();
            SceneTransitionManager.BeginBlackHold();
            SceneTransitionManager.LoadSceneWithFade(target.sceneName);
            return true;
        }

        public bool DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath))
                    File.Delete(SavePath);

                _loadedSave = null;
                _pendingBonfireSpawn = null;

                if (debugLog)
                    Debug.Log("[SaveManager] Deleted save file.");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Delete save failed: {ex.Message}");
                return false;
            }
        }

        [ContextMenu("Debug Save Game")]
        private void DebugSaveGame()
        {
            SaveGame();
        }

        [ContextMenu("Debug Load Game")]
        private void DebugLoadGame()
        {
            LoadGame();
        }

        [ContextMenu("Debug Delete Save")]
        private void DebugDeleteSave()
        {
            DeleteSave();
        }

        [ContextMenu("Debug Print Save State")]
        public void DebugPrintSaveState()
        {
            Debug.Log(GetDebugSummary());
        }

        [ContextMenu("Debug Force Respawn At Saved Bonfire")]
        public void DebugForceRespawnAtSavedBonfire()
        {
            ForceRespawnAtSavedBonfireForDebug();
        }

        public bool ForceRespawnAtSavedBonfireForDebug()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SaveManager] Force respawn can only run in Play Mode.");
                return false;
            }

            GameSaveData data = _loadedSave ?? ReadSaveData();
            if (data == null || !data.HasBonfire)
            {
                Debug.LogWarning("[SaveManager] Force respawn failed: no saved bonfire.");
                return false;
            }

            _loadedSave = data;
            _pendingBonfireSpawn = data.bonfire;

            Debug.Log($"[SaveManager] Debug force respawn requested. Target: {data.bonfire.bonfireId}, scene {data.bonfire.sceneName}, position {data.bonfire.position.ToVector3()}.");

            if (SceneManager.GetActiveScene().name != data.bonfire.sceneName)
            {
                EditorSelectionGuard.ClearSelection();
                SceneTransitionManager.BeginBlackHold();
                SceneTransitionManager.LoadSceneWithFade(data.bonfire.sceneName);
            }
            else
            {
                SceneTransitionManager.BeginBlackHold();
                QueuePendingBonfireSpawn();
            }

            return true;
        }

        public string GetDebugSummary()
        {
            GameSaveData data = _loadedSave;
            if (data == null && HasSaveFile())
                data = ReadSaveData();

            GameObject player = FindPlayerObject();
            BonfireSaveData bonfire = data?.bonfire;

            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine("KAM Save Debug");
            builder.AppendLine($"Save Path: {SavePath}");
            builder.AppendLine($"Has Save File: {HasSaveFile()}");
            builder.AppendLine($"Loaded Save: {_loadedSave != null}");
            builder.AppendLine($"Active Scene: {SceneManager.GetActiveScene().name}");
            builder.AppendLine($"Is Respawning From Death: {_isRespawningFromDeath}");
            builder.AppendLine($"Pending Bonfire: {(_pendingBonfireSpawn != null ? _pendingBonfireSpawn.bonfireId : "None")}");
            builder.AppendLine($"Player Position: {(player != null ? player.transform.position.ToString() : "Player not found")}");

            if (bonfire != null)
            {
                builder.AppendLine($"Last Bonfire ID: {bonfire.bonfireId}");
                builder.AppendLine($"Last Bonfire Scene: {bonfire.sceneName}");
                builder.AppendLine($"Last Bonfire Position: {bonfire.position.ToVector3()}");
                builder.AppendLine($"Last Bonfire Rotation: {bonfire.eulerAngles.ToVector3()}");
            }
            else
            {
                builder.AppendLine("Last Bonfire: None");
            }

            return builder.ToString();
        }

        private GameSaveData CaptureCurrentState()
        {
            GameSaveData data = new GameSaveData
            {
                activeSceneName = SceneManager.GetActiveScene().name,
                inventory = CaptureInventory(),
                progression = CaptureProgression(),
                discoveredBonfires = _loadedSave?.discoveredBonfires,
                scenes = UpdateSceneSaveData(_loadedSave?.scenes, CaptureActiveSceneState())
            };

            return data;
        }

        private InventorySaveData CaptureInventory()
        {
            return new InventorySaveData
            {
                spiritFragments = InventoryManager.Instance != null ? InventoryManager.Instance.SpiritFragments : 0
            };
        }

        private PlayerProgression.ProgressionSaveData CaptureProgression()
        {
            return PlayerProgression.Instance != null
                ? PlayerProgression.Instance.GetSaveData()
                : new PlayerProgression.ProgressionSaveData();
        }

        private void ApplyPersistentState(GameSaveData data)
        {
            if (data == null)
                return;

            if (InventoryManager.Instance != null && data.inventory != null)
                InventoryManager.Instance.LoadSaveData(data.inventory);

            if (PlayerProgression.Instance != null)
                PlayerProgression.Instance.LoadSaveData(data.progression);
        }

        private GameSaveData ReadSaveData()
        {
            if (!HasSaveFile())
            {
                if (debugLog)
                    Debug.LogWarning("[SaveManager] No save file found.");

                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null)
                    Debug.LogError("[SaveManager] Save file could not be parsed.");

                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Read save failed: {ex.Message}");
                return null;
            }
        }

        private bool WriteSaveData(GameSaveData data)
        {
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string tempPath = SavePath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(SavePath))
                    File.Delete(SavePath);

                File.Move(tempPath, SavePath);

                if (debugLog)
                    Debug.Log($"[SaveManager] Saved game to {SavePath}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
                return false;
            }
        }

        private int ApplyDeathPenalty(GameSaveData data)
        {
            if (data.inventory == null)
                data.inventory = new InventorySaveData();

            if (InventoryManager.Instance != null)
            {
                int lostFragments = InventoryManager.Instance.LoseSpiritFragmentsPercent(deathSpiritFragmentLossPercent);
                data.inventory = InventoryManager.Instance.GetSaveData();
                return lostFragments;
            }

            int loss = Mathf.CeilToInt(data.inventory.spiritFragments * deathSpiritFragmentLossPercent);
            data.inventory.spiritFragments = Mathf.Max(0, data.inventory.spiritFragments - loss);
            return loss;
        }

        private BonfireSaveData[] UpsertDiscoveredBonfire(BonfireSaveData[] bonfires, BonfireSaveData bonfire)
        {
            if (bonfire == null || string.IsNullOrWhiteSpace(bonfire.bonfireId))
                return bonfires;

            List<BonfireSaveData> discovered = bonfires != null
                ? new List<BonfireSaveData>(bonfires)
                : new List<BonfireSaveData>();

            int index = discovered.FindIndex(item => item != null && item.bonfireId == bonfire.bonfireId);
            if (index >= 0)
                discovered[index] = bonfire;
            else
                discovered.Add(bonfire);

            return discovered.ToArray();
        }

        private BonfireSaveData FindDiscoveredBonfire(BonfireSaveData[] bonfires, string bonfireId)
        {
            if (bonfires == null)
                return null;

            foreach (BonfireSaveData bonfire in bonfires)
            {
                if (bonfire != null && bonfire.bonfireId == bonfireId)
                    return bonfire;
            }

            return null;
        }

        private SceneSaveData CaptureActiveSceneState()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            Scene activeScene = SceneManager.GetActiveScene();
            List<SaveableObjectData> objects = new List<SaveableObjectData>();
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour.gameObject.scene != activeScene)
                    continue;

                if (behaviour is not ISaveable saveable)
                    continue;

                SaveableId saveableId = behaviour.GetComponent<SaveableId>();
                if (saveableId == null || !saveableId.HasId)
                {
                    if (debugLog)
                        Debug.LogWarning($"[SaveManager] Saveable '{behaviour.name}' has no SaveableId. State skipped.");
                    continue;
                }

                objects.Add(new SaveableObjectData
                {
                    saveableId = saveableId.Id,
                    type = saveable.SaveableType,
                    json = saveable.CaptureStateJson()
                });
            }

            return new SceneSaveData
            {
                sceneName = sceneName,
                objects = objects.ToArray()
            };
        }

        private SceneSaveData[] UpdateSceneSaveData(SceneSaveData[] existingScenes, SceneSaveData currentSceneData)
        {
            if (currentSceneData == null)
                return existingScenes;

            List<SceneSaveData> scenes = existingScenes != null
                ? new List<SceneSaveData>(existingScenes)
                : new List<SceneSaveData>();

            int index = scenes.FindIndex(scene => scene != null && scene.sceneName == currentSceneData.sceneName);
            if (index >= 0)
                scenes[index] = currentSceneData;
            else
                scenes.Add(currentSceneData);

            return scenes.ToArray();
        }

        private void ApplySceneState(SceneSaveData sceneData)
        {
            if (sceneData == null || sceneData.objects == null)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            Dictionary<string, SaveableObjectData> stateById = new Dictionary<string, SaveableObjectData>();
            foreach (SaveableObjectData objectData in sceneData.objects)
            {
                if (objectData == null || string.IsNullOrWhiteSpace(objectData.saveableId))
                    continue;

                stateById[objectData.saveableId] = objectData;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour.gameObject.scene != activeScene)
                    continue;

                if (behaviour is not ISaveable saveable)
                    continue;

                SaveableId saveableId = behaviour.GetComponent<SaveableId>();
                if (saveableId == null || !saveableId.HasId)
                    continue;

                if (stateById.TryGetValue(saveableId.Id, out SaveableObjectData objectData))
                {
                    saveable.RestoreStateJson(objectData.json);
                }
            }
        }

        private SceneSaveData GetSceneSaveData(GameSaveData data, string sceneName)
        {
            if (data?.scenes == null)
                return null;

            foreach (SceneSaveData sceneData in data.scenes)
            {
                if (sceneData != null && sceneData.sceneName == sceneName)
                    return sceneData;
            }

            return null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_loadedSave != null)
            {
                ApplyPersistentState(_loadedSave);
                ApplySceneState(GetSceneSaveData(_loadedSave, scene.name));
            }

            if (_pendingBonfireSpawn != null && _pendingBonfireSpawn.sceneName == scene.name)
                QueuePendingBonfireSpawn();
        }

        private void QueuePendingBonfireSpawn()
        {
            if (_pendingBonfireSpawnRoutine != null)
                StopCoroutine(_pendingBonfireSpawnRoutine);

            _pendingBonfireSpawnRoutine = StartCoroutine(ApplyPendingBonfireSpawnWhenSceneIsReady());
        }

        private System.Collections.IEnumerator ApplyPendingBonfireSpawnWhenSceneIsReady()
        {
            // Scene-loaded callbacks run before Start on scene objects. Waiting lets scene
            // managers finish their own default spawn logic before the bonfire spawn wins.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (SceneTransitionManager.IsHoldingBlack)
                yield return SceneTransitionManager.WaitUntilBlack();

            const int maxAttempts = 20;
            bool spawned = false;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (ApplyPendingBonfireSpawn())
                {
                    spawned = true;
                    break;
                }

                yield return null;
            }

            if (!spawned)
                SceneTransitionManager.EndBlackHold(0f);

            _pendingBonfireSpawnRoutine = null;
        }

        private bool ApplyPendingBonfireSpawn()
        {
            if (_pendingBonfireSpawn == null)
                return true;

            GameObject player = FindPlayerObject();

            if (player == null)
            {
                Debug.LogWarning("[SaveManager] Could not find player for bonfire spawn.");
                return false;
            }

            Vector3 beforePosition = player.transform.position;
            Vector3 targetPosition = _pendingBonfireSpawn.position.ToVector3();

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;

            player.transform.SetPositionAndRotation(
                targetPosition,
                Quaternion.Euler(_pendingBonfireSpawn.eulerAngles.ToVector3())
            );

            if (characterController != null)
                characterController.enabled = true;

            RestorePlayerToFullHealth();

            if (debugLog)
                Debug.Log($"[SaveManager] Spawned player at bonfire '{_pendingBonfireSpawn.bonfireId}'. Moved from {beforePosition} to {player.transform.position}.");

            _pendingBonfireSpawn = null;
            _isRespawningFromDeath = false;
            SceneTransitionManager.EndBlackHold();
            return true;
        }

        private GameObject FindPlayerObject()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                return player;

            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            return movement != null ? movement.gameObject : null;
        }

        private void RestorePlayerToFullHealth()
        {
            PlayerCombatUnit player = FindFirstObjectByType<PlayerCombatUnit>();
            if (player != null && player.Stats != null)
            {
                player.Heal(player.Stats.MaxHealth);
                player.ClearAllEffects();
            }
        }
    }
}
