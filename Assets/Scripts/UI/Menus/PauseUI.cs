using KAM.UI.Feedback;
using KAM.UI.SkillTree;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KAM.UI.Menus
{
    public class PauseUI : MonoBehaviour
    {
        private static PauseUI _instance;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "Start";

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        [Header("Settings")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private CanvasGroup _canvasGroup;
        private Button _resumeButton;
        private float _previousTimeScale = 1f;
        private bool _isPaused;

        public static bool IsPaused => _instance != null && _instance._isPaused;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (dontDestroyOnLoad)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            if (buildOnAwake && transform.childCount == 0)
                BuildUI();
            else
                BindExistingUI();

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (toggleKey == KeyCode.None || !Input.GetKeyDown(toggleKey))
                return;

            if (DeathUI.IsShowing)
                return;

            if (SceneManager.GetActiveScene().name == mainMenuSceneName)
                return;

            if (SkillTreeUI.CloseOpenInstance() || SkillTreeUI.WasClosedThisFrame)
                return;

            if (_isPaused)
                Resume();
            else
                Show();
        }

        [ContextMenu("Rebuild Pause UI")]
        public void RebuildUI()
        {
            BuildUI();
            HideImmediate();
        }

        public void Show()
        {
            if (_isPaused)
                return;

            _previousTimeScale = Time.timeScale;
            if (_previousTimeScale <= 0f)
                _previousTimeScale = 1f;

            _isPaused = true;
            Time.timeScale = 0f;

            if (_canvasGroup == null)
                BindExistingUI();

            if (_canvasGroup == null)
                BuildUI();

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            EnsureEventSystem();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void Resume()
        {
            if (!_isPaused)
                return;

            Time.timeScale = _previousTimeScale;
            HideImmediate();
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            HideImmediate();
            SceneTransitionManager.LoadSceneWithFade(mainMenuSceneName);
        }

        private void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HideImmediate()
        {
            _isPaused = false;

            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void BuildUI()
        {
            ClearChildren(transform);

            Canvas canvas = GetOrAddComponent<Canvas>(gameObject);
            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(gameObject);
            KamModalStyle.ConfigureCanvas(canvas, scaler, 7500);
            GetOrAddComponent<GraphicRaycaster>(gameObject);
            _canvasGroup = GetOrAddComponent<CanvasGroup>(gameObject);

            GameObject backdrop = KamModalStyle.CreateUIObject("Backdrop", transform);
            Stretch(backdrop.GetComponent<RectTransform>());
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = KamModalStyle.BackdropColor;

            GameObject panel = KamModalStyle.CreateUIObject("Panel", backdrop.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(500f, 500f);
            KamModalStyle.AddImage(panel, KamModalStyle.PanelSprite, KamModalStyle.PanelFallbackColor);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(46, 46, 46, 46);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject header = KamModalStyle.CreateUIObject("Header", panel.transform);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 86f;
            KamModalStyle.AddImage(header, KamModalStyle.HeaderSprite, KamModalStyle.HeaderFallbackColor);

            TextMeshProUGUI title = AddPanelText("Title", header.transform, "PAUSED", 42f, KamModalStyle.Gold, FontStyles.Bold);
            Stretch(title.rectTransform, new Vector2(16f, 8f), new Vector2(-16f, -8f));

            AddSpacer(panel.transform, 10f);
            _resumeButton = CreateButton("RESUME", panel.transform, Resume);
            CreateButton("MAIN MENU", panel.transform, ReturnToMainMenu);
            CreateButton("QUIT", panel.transform, QuitGame);

            AddSpacer(panel.transform, 12f);
            GameObject glyph = KamModalStyle.CreateUIObject("KamGlyph", panel.transform);
            LayoutElement glyphLayout = glyph.AddComponent<LayoutElement>();
            glyphLayout.preferredHeight = 56f;
            RectTransform glyphRect = glyph.GetComponent<RectTransform>();
            glyphRect.sizeDelta = new Vector2(56f, 56f);
            KamModalStyle.AddImage(glyph, KamModalStyle.KamGlyph, Color.clear, true);

            EnsureEventSystem();
            BindExistingUI();
        }

        private void BindExistingUI()
        {
            _canvasGroup = GetOrAddComponent<CanvasGroup>(gameObject);
            _resumeButton = FindButton("RESUMEButton");
            Button mainMenuButton = FindButton("MAINMENUButton");
            Button quitButton = FindButton("QUITButton");

            BindButton(_resumeButton, Resume);
            BindButton(mainMenuButton, ReturnToMainMenu);
            BindButton(quitButton, QuitGame);

            GetOrAddComponent<Canvas>(gameObject);
            GetOrAddComponent<CanvasScaler>(gameObject);
            GetOrAddComponent<GraphicRaycaster>(gameObject);
            EnsureEventSystem();
        }

        private Button FindButton(string objectName)
        {
            Transform target = FindChildRecursive(transform, objectName);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction onClick)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                ClearSelectedGameObject();
                onClick?.Invoke();
                ClearSelectedGameObject();
            });
        }

        private static void ClearSelectedGameObject()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private static TextMeshProUGUI AddPanelText(string name, Transform parent, string value, float size, Color color, FontStyles style)
        {
            GameObject obj = KamModalStyle.CreateUIObject(name, parent);
            TextMeshProUGUI text = KamModalStyle.AddText(obj, value, size, color, TextAlignmentOptions.Center, style);
            LayoutElement layout = obj.AddComponent<LayoutElement>();
            layout.preferredHeight = size * 1.45f;
            return text;
        }

        private static Button CreateButton(string label, Transform parent, UnityEngine.Events.UnityAction onClick)
        {
            GameObject obj = KamModalStyle.CreateUIObject($"{label.Replace(" ", string.Empty)}Button", parent);
            LayoutElement layout = obj.AddComponent<LayoutElement>();
            layout.preferredWidth = 330f;
            layout.preferredHeight = 68f;

            Button button = KamModalStyle.AddStyledButton(obj, onClick);

            TextMeshProUGUI text = AddPanelText("Label", obj.transform, label, 22f, KamModalStyle.Gold, FontStyles.Bold);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void AddSpacer(Transform parent, float height)
        {
            GameObject spacer = KamModalStyle.CreateUIObject("Spacer", parent);
            LayoutElement layout = spacer.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void EnsureEventSystem()
        {
            EventSystem[] systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem active = EventSystem.current;

            if (active == null && systems.Length > 0)
                active = systems[0];

            if (active == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                active = eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
                systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            else
            {
                EnsureInputSystemModule(active);
            }

            foreach (EventSystem system in systems)
            {
                if (system == null || system == active)
                    continue;

                if (Application.isPlaying)
                    Destroy(system.gameObject);
                else
                    DestroyImmediate(system.gameObject);
            }
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

        private static T GetOrAddComponent<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            return component != null ? component : obj.AddComponent<T>();
        }

        private static Transform FindChildRecursive(Transform parent, string objectName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == objectName)
                    return child;

                Transform nested = FindChildRecursive(child, objectName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
