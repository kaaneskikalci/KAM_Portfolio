using KAM.Core.Combat;
using KAM.Interaction;
using KAM.UI.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KAM.SaveSystem
{
    public class BonfireSavePoint : BaseInteractable
    {
        [Header("Bonfire Save")]
        [SerializeField] private string bonfireId = "bonfire_01";
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private bool restorePlayerHealth = true;
        [SerializeField] private bool saveOnInteract = true;
        [SerializeField] private bool useBonfireMenu = true;

        [Header("UI Audio")]
        [SerializeField] private AudioClip menuOpenSound;
        [SerializeField] private AudioClip menuCloseSound;
        [SerializeField, Range(0f, 1f)] private float menuSoundVolume = 1f;

#if UNITY_EDITOR
        private const string DefaultMenuOpenSoundPath = "Assets/Assets/Sfx/ui opening.mp3";
        private const string DefaultMenuCloseSoundPath = "Assets/Assets/Sfx/ui close.mp3";
#endif

        public string BonfireId => bonfireId;
        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
        public string SceneName => SceneManager.GetActiveScene().name;
        public AudioClip MenuOpenSound => menuOpenSound;
        public AudioClip MenuCloseSound => menuCloseSound;
        public float MenuSoundVolume => menuSoundVolume;

        protected override void Awake()
        {
            base.Awake();

            interactionPrompt = string.IsNullOrWhiteSpace(interactionPrompt) || interactionPrompt == "Interact"
                ? "Rest at Bonfire"
                : interactionPrompt;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (menuOpenSound == null)
            {
                menuOpenSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultMenuOpenSoundPath);
            }

            if (menuCloseSound == null)
            {
                menuCloseSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultMenuCloseSoundPath);
            }
        }
#endif

        protected override void OnInteract(GameObject interactor)
        {
            if (useBonfireMenu)
            {
                BonfireMenuUI.EnsureInstance().Show(this, interactor);
                return;
            }

            Rest(interactor);
        }

        public void Rest(GameObject interactor)
        {
            SaveManager manager = SaveManager.EnsureInstance();

            if (saveOnInteract)
            {
                manager.RestAtBonfire(this);
                return;
            }

            manager.RecordBonfire(this);

            if (restorePlayerHealth)
                RestorePlayer(interactor);
        }

        public BonfireSaveData ToSaveData()
        {
            Transform point = SpawnPoint;
            return new BonfireSaveData
            {
                bonfireId = bonfireId,
                sceneName = SceneName,
                position = new SerializableVector3(point.position),
                eulerAngles = new SerializableVector3(point.rotation.eulerAngles)
            };
        }

        [ContextMenu("Validate Bonfire Setup")]
        public void ValidateBonfireSetup()
        {
            bool valid = true;

            if (string.IsNullOrWhiteSpace(bonfireId))
            {
                Debug.LogError($"[BonfireSavePoint] '{name}' has an empty Bonfire ID.", this);
                valid = false;
            }

            if (spawnPoint == null)
            {
                Debug.LogWarning($"[BonfireSavePoint] '{name}' has no Spawn Point assigned. It will use the bonfire object's own transform at {transform.position}.", this);
            }

            BonfireSavePoint[] bonfires = FindObjectsByType<BonfireSavePoint>(FindObjectsInactive.Include);
            int duplicateCount = 0;
            foreach (BonfireSavePoint bonfire in bonfires)
            {
                if (bonfire != null && bonfire != this && bonfire.bonfireId == bonfireId)
                    duplicateCount++;
            }

            if (duplicateCount > 0)
            {
                Debug.LogError($"[BonfireSavePoint] Duplicate Bonfire ID '{bonfireId}' found in scene. Each bonfire needs a unique ID.", this);
                valid = false;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float distance = Vector3.Distance(player.transform.position, SpawnPoint.position);
                Debug.Log($"[BonfireSavePoint] Player distance to spawn '{bonfireId}': {distance:0.00}. Player={player.transform.position}, Spawn={SpawnPoint.position}", this);
            }

            if (valid)
            {
                Debug.Log($"[BonfireSavePoint] '{bonfireId}' setup looks valid. Scene={SceneName}, Spawn={SpawnPoint.position}, Rotation={SpawnPoint.rotation.eulerAngles}", this);
            }
        }

        private void RestorePlayer(GameObject interactor)
        {
            PlayerCombatUnit player = interactor != null
                ? interactor.GetComponentInParent<PlayerCombatUnit>()
                : null;

            if (player == null)
                player = FindFirstObjectByType<PlayerCombatUnit>();

            if (player != null && player.Stats != null)
            {
                player.Heal(player.Stats.MaxHealth);
                player.ClearAllEffects();
            }
        }
    }

    public class BonfireMenuUI : MonoBehaviour
    {
        private static BonfireMenuUI _instance;

        private BonfireSavePoint _currentBonfire;
        private GameObject _currentInteractor;
        private RectTransform _panel;
        private AudioSource _audioSource;
        private static AudioSource _sharedAudioSource;

        public static BonfireMenuUI EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject menuObject = new GameObject("BonfireMenuUI");
            _instance = menuObject.AddComponent<BonfireMenuUI>();
            DontDestroyOnLoad(menuObject);
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildUI();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        public void Show(BonfireSavePoint bonfire, GameObject interactor)
        {
            _currentBonfire = bonfire;
            _currentInteractor = interactor;
            gameObject.SetActive(true);
            PlayUISound(_currentBonfire?.MenuOpenSound, _currentBonfire != null ? _currentBonfire.MenuSoundVolume : 1f);
        }

        public void Close()
        {
            PlayUISound(_currentBonfire?.MenuCloseSound, _currentBonfire != null ? _currentBonfire.MenuSoundVolume : 1f);
            gameObject.SetActive(false);
        }

        private void BuildUI()
        {
            EnsureEventSystem();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            KamModalStyle.ConfigureCanvas(canvas, scaler, 5000);
            gameObject.AddComponent<GraphicRaycaster>();
            EnsureAudioSource();

            GameObject backdrop = CreateUIObject("Backdrop", transform);
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = KamModalStyle.BackdropColor;

            GameObject panelObject = CreateUIObject("Panel", backdrop.transform);
            _panel = panelObject.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(460f, 430f);
            _panel.anchoredPosition = Vector2.zero;

            KamModalStyle.AddImage(panelObject, KamModalStyle.PanelSprite, KamModalStyle.PanelFallbackColor);

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(38, 38, 34, 34);
            layout.spacing = 6f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            GameObject header = CreateUIObject("Header", _panel);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(320f, 64f);
            KamModalStyle.AddImage(header, KamModalStyle.HeaderSprite, KamModalStyle.HeaderFallbackColor);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredWidth = 320f;
            headerLayout.preferredHeight = 64f;
            TextMeshProUGUI headerText = CreateLabel("BONFIRE", header.transform, 24, TextAlignmentOptions.Center);
            headerText.color = KamModalStyle.Gold;
            RectTransform headerTextRect = headerText.GetComponent<RectTransform>();
            headerTextRect.anchorMin = Vector2.zero;
            headerTextRect.anchorMax = Vector2.one;
            headerTextRect.offsetMin = new Vector2(18f, 8f);
            headerTextRect.offsetMax = new Vector2(-18f, -8f);

            GameObject glyph = CreateUIObject("KamGlyph", _panel);
            RectTransform glyphRect = glyph.GetComponent<RectTransform>();
            glyphRect.sizeDelta = new Vector2(46f, 46f);
            KamModalStyle.AddImage(glyph, KamModalStyle.KamGlyph, Color.clear, true);
            LayoutElement glyphLayout = glyph.AddComponent<LayoutElement>();
            glyphLayout.preferredWidth = 46f;
            glyphLayout.preferredHeight = 46f;

            GameObject buttonGroup = CreateUIObject("ButtonGroup", _panel);
            RectTransform buttonGroupRect = buttonGroup.GetComponent<RectTransform>();
            buttonGroupRect.sizeDelta = new Vector2(350f, 113f);
            LayoutElement buttonGroupLayout = buttonGroup.AddComponent<LayoutElement>();
            buttonGroupLayout.preferredWidth = 350f;
            buttonGroupLayout.preferredHeight = 113f;
            VerticalLayoutGroup buttonGroupVertical = buttonGroup.AddComponent<VerticalLayoutGroup>();
            buttonGroupVertical.spacing = 9f;
            buttonGroupVertical.childAlignment = TextAnchor.MiddleCenter;
            buttonGroupVertical.childControlWidth = false;
            buttonGroupVertical.childControlHeight = false;
            buttonGroupVertical.childForceExpandWidth = false;
            buttonGroupVertical.childForceExpandHeight = false;

            CreateButton("Rest", buttonGroup.transform, HandleRest);
            CreateButton("Leave", buttonGroup.transform, Close);
        }

        private void HandleRest()
        {
            BonfireSavePoint bonfire = _currentBonfire;
            GameObject interactor = _currentInteractor;
            Close();
            bonfire?.Rest(interactor);
        }

        private void PlayUISound(AudioClip clip, float volume)
        {
            if (clip == null) return;

            EnsureAudioSource();

            if (_audioSource == null) return;

            _audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void EnsureAudioSource()
        {
            if (_sharedAudioSource != null)
            {
                _audioSource = _sharedAudioSource;
                return;
            }

            GameObject audioObject = new GameObject("BonfireMenuAudioSource");
            DontDestroyOnLoad(audioObject);
            _sharedAudioSource = audioObject.AddComponent<AudioSource>();

            _sharedAudioSource.playOnAwake = false;
            _sharedAudioSource.loop = false;
            _sharedAudioSource.spatialBlend = 0f;

            _audioSource = _sharedAudioSource;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static TextMeshProUGUI CreateLabel(string text, Transform parent, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject labelObject = CreateUIObject("Label", parent);
            TextMeshProUGUI label = KamModalStyle.AddText(labelObject, text, fontSize, KamModalStyle.Bone, alignment);

            LayoutElement layout = labelObject.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize + 12f;
            return label;
        }

        private static Button CreateButton(string text, Transform parent, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreateUIObject(text, parent);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(330f, 52f);
            Button button = KamModalStyle.AddStyledButton(buttonObject, onClick);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 330f;
            layout.preferredHeight = 52f;

            TextMeshProUGUI label = CreateLabel(text, buttonObject.transform, 18, TextAlignmentOptions.Center);
            label.color = KamModalStyle.Gold;
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                EnsureInputSystemModule(eventSystem);
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static void EnsureInputSystemModule(EventSystem eventSystem)
        {
            if (eventSystem == null)
                return;

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                if (Application.isPlaying)
                    Destroy(standalone);
                else
                    DestroyImmediate(standalone);
            }
        }
    }
}
