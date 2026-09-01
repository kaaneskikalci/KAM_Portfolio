using KAM.SaveSystem;
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

namespace KAM.UI.Menus
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string newGameSceneName = "OBA";

        [Header("Labels")]
        [SerializeField] private string titleText = "KAM";
        [SerializeField] private string subtitleText = "Enter the Oba";

        [Header("Settings")]
        [SerializeField] private bool buildOnAwake = true;

        private Button _continueButton;

        private void Awake()
        {
            Time.timeScale = 1f;

            if (buildOnAwake && transform.childCount == 0)
                BuildUI();
            else
                BindExistingUI();
        }

        private void OnEnable()
        {
            RefreshContinueButton();
        }

        [ContextMenu("Rebuild Main Menu UI")]
        public void RebuildUI()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            ClearChildren(transform);

            Canvas canvas = GetOrAddComponent<Canvas>(gameObject);
            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(gameObject);
            KamModalStyle.ConfigureCanvas(canvas, scaler, 1000);
            GetOrAddComponent<GraphicRaycaster>(gameObject);

            GameObject backdrop = KamModalStyle.CreateUIObject("Backdrop", transform);
            Stretch(backdrop.GetComponent<RectTransform>());
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0.012f, 0.01f, 0.008f, 0.92f);

            GameObject panel = KamModalStyle.CreateUIObject("Panel", backdrop.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(620f, 650f);
            KamModalStyle.AddImage(panel, KamModalStyle.PanelSprite, KamModalStyle.PanelFallbackColor);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 56, 56);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject header = KamModalStyle.CreateUIObject("Header", panel.transform);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 110f;
            KamModalStyle.AddImage(header, KamModalStyle.HeaderSprite, KamModalStyle.HeaderFallbackColor);

            TextMeshProUGUI title = AddPanelText("Title", header.transform, titleText, 58f, KamModalStyle.Gold, FontStyles.Bold);
            Stretch(title.rectTransform, new Vector2(18f, 10f), new Vector2(-18f, -10f));

            TextMeshProUGUI subtitle = AddPanelText("Subtitle", panel.transform, subtitleText, 22f, KamModalStyle.Bone, FontStyles.Normal);
            subtitle.GetComponent<LayoutElement>().preferredHeight = 42f;

            AddSpacer(panel.transform, 8f);
            CreateButton("NEW GAME", panel.transform, HandleNewGameClicked);
            _continueButton = CreateButton("CONTINUE", panel.transform, HandleContinueClicked);
            CreateButton("QUIT", panel.transform, HandleQuitClicked);

            AddSpacer(panel.transform, 16f);
            GameObject glyph = KamModalStyle.CreateUIObject("KamGlyph", panel.transform);
            LayoutElement glyphLayout = glyph.AddComponent<LayoutElement>();
            glyphLayout.preferredHeight = 76f;
            RectTransform glyphRect = glyph.GetComponent<RectTransform>();
            glyphRect.sizeDelta = new Vector2(76f, 76f);
            KamModalStyle.AddImage(glyph, KamModalStyle.KamGlyph, Color.clear, true);

            EnsureEventSystem();
            BindExistingUI();
            RefreshContinueButton();
        }

        private void HandleNewGameClicked()
        {
            Time.timeScale = 1f;
            SaveManager.EnsureInstance().DeleteSave();
            SceneTransitionManager.LoadSceneWithFade(newGameSceneName);
        }

        private void HandleContinueClicked()
        {
            Time.timeScale = 1f;
            SaveManager saveManager = SaveManager.EnsureInstance();
            if (!saveManager.LoadGame(loadSavedScene: true))
            {
                Debug.LogWarning("[MainMenuUI] Continue requested but no valid save was found.");
                RefreshContinueButton();
            }
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RefreshContinueButton()
        {
            if (_continueButton == null)
                BindExistingUI();

            if (_continueButton == null)
                return;

            if (!Application.isPlaying)
            {
                _continueButton.interactable = true;
                return;
            }

            bool hasSave = SaveManager.EnsureInstance().HasSaveFile();
            _continueButton.interactable = hasSave;
        }

        private void BindExistingUI()
        {
            Button newGameButton = FindButton("NEWGAMEButton");
            _continueButton = FindButton("CONTINUEButton");
            Button quitButton = FindButton("QUITButton");

            BindButton(newGameButton, HandleNewGameClicked);
            BindButton(_continueButton, HandleContinueClicked);
            BindButton(quitButton, HandleQuitClicked);

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
            layout.preferredWidth = 390f;
            layout.preferredHeight = 74f;

            Button button = KamModalStyle.AddStyledButton(obj, onClick);

            TextMeshProUGUI text = AddPanelText("Label", obj.transform, label, 25f, KamModalStyle.Gold, FontStyles.Bold);
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
