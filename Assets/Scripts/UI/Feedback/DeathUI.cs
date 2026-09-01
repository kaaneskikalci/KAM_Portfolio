using System;
using KAM.Core;
using KAM.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KAM.UI.Feedback
{
    public class DeathUI : MonoBehaviour
    {
        private static DeathUI _instance;

        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _bodyText;
        private Button _respawnButton;
        private Action _fallbackAction;
        private bool _isVisible;
        private Coroutine _showRoutine;

        [Header("Timing")]
        [SerializeField] private float showDelaySeconds = 1.25f;

        public static bool IsShowing => _instance != null && (_instance._isVisible || _instance._showRoutine != null);

        public static DeathUI EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject obj = new GameObject("DeathUI");
            _instance = obj.AddComponent<DeathUI>();
            DontDestroyOnLoad(obj);
            return _instance;
        }

        public static bool ShowRespawnOrFallback(Action fallbackAction = null)
        {
            if (IsShowing)
                return true;

            DeathUI deathUI = EnsureInstance();
            deathUI.ShowDelayed(fallbackAction);
            return true;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            HideImmediate();
        }

        public void Show(Action fallbackAction = null)
        {
            _showRoutine = null;
            _fallbackAction = fallbackAction;
            _isVisible = true;
            Time.timeScale = 0f;

            SaveManager saveManager = SaveManager.EnsureInstance();
            string bonfireText = "Respawn at the last bonfire.";
            if (saveManager.LoadedSave?.bonfire != null)
            {
                bonfireText = $"Respawn at {saveManager.LoadedSave.bonfire.bonfireId}.";
            }

            _bodyText.text = $"{bonfireText}\nHalf of your Spirit Fragments will be lost.";
            BindRespawnButton();

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }
            else
            {
                EnsureInputSystemModule(eventSystem);
            }

            eventSystem.SetSelectedGameObject(_respawnButton.gameObject);
        }

        private void ShowDelayed(Action fallbackAction)
        {
            _fallbackAction = fallbackAction;

            if (_showRoutine != null)
                StopCoroutine(_showRoutine);

            _showRoutine = StartCoroutine(ShowAfterDelay());
        }

        private System.Collections.IEnumerator ShowAfterDelay()
        {
            float delay = Mathf.Max(0f, showDelaySeconds);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            Show(_fallbackAction);
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

        private void HandleRespawnClicked()
        {
            if (!_isVisible)
                return;

            HideImmediate();
            Time.timeScale = 1f;

            SaveManager saveManager = SaveManager.EnsureInstance();
            Debug.Log("[DeathUI] Respawn clicked. Requesting last bonfire respawn.");
            if (!saveManager.RespawnAtLastBonfireAfterDeath())
            {
                _fallbackAction?.Invoke();
                if (_fallbackAction == null && GameManager.Instance != null)
                    GameManager.Instance.EndRun(RunEndReason.Defeat);
            }
        }

        private void HideImmediate()
        {
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }

            _isVisible = false;
            _fallbackAction = null;

            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void BuildUI()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            KamModalStyle.ConfigureCanvas(canvas, scaler, 8000);
            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            GameObject backdrop = CreateUIObject("Backdrop", transform);
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0.01f, 0.006f, 0.004f, 0.88f);

            GameObject panel = CreateUIObject("Panel", backdrop.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(540f, 540f);
            panelRect.anchoredPosition = Vector2.zero;
            KamModalStyle.AddImage(panel, KamModalStyle.PanelSprite, KamModalStyle.PanelFallbackColor);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(44, 44, 46, 46);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject header = CreateUIObject("Header", panel.transform);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(360f, 90f);
            KamModalStyle.AddImage(header, KamModalStyle.HeaderSprite, KamModalStyle.HeaderFallbackColor);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 90f;

            TextMeshProUGUI title = CreateText("Title", header.transform, "YOU DIED", 56, KamModalStyle.Red);
            title.fontStyle = FontStyles.Bold;
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(18f, 10f);
            titleRect.offsetMax = new Vector2(-18f, -10f);

            GameObject skull = CreateUIObject("SkullGlyph", panel.transform);
            RectTransform skullRect = skull.GetComponent<RectTransform>();
            skullRect.sizeDelta = new Vector2(58f, 58f);
            KamModalStyle.AddImage(skull, KamModalStyle.SkullGlyph, Color.clear, true);
            LayoutElement skullLayout = skull.AddComponent<LayoutElement>();
            skullLayout.preferredHeight = 58f;

            _bodyText = CreateText("Body", panel.transform, string.Empty, 21, KamModalStyle.Bone);
            LayoutElement bodyLayout = _bodyText.GetComponent<LayoutElement>();
            bodyLayout.preferredHeight = 74f;

            _respawnButton = CreateButton("RESPAWN", panel.transform);
            BindRespawnButton();
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string textValue, int fontSize, Color color)
        {
            GameObject obj = CreateUIObject(name, parent);
            TextMeshProUGUI text = KamModalStyle.AddText(obj, textValue, fontSize, color, TextAlignmentOptions.Center);

            LayoutElement layout = obj.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize * 1.7f;
            return text;
        }

        private static Button CreateButton(string label, Transform parent)
        {
            GameObject obj = CreateUIObject("RespawnButton", parent);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 82f);

            Button button = KamModalStyle.AddStyledButton(obj, null);

            LayoutElement layout = obj.AddComponent<LayoutElement>();
            layout.preferredWidth = 260f;
            layout.preferredHeight = 82f;

            TextMeshProUGUI text = CreateText("Label", obj.transform, label, 23, KamModalStyle.Gold);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;

            return button;
        }

        private void BindRespawnButton()
        {
            if (_respawnButton == null)
                return;

            _respawnButton.onClick.RemoveAllListeners();
            _respawnButton.onClick.AddListener(HandleRespawnClicked);
        }
    }
}
