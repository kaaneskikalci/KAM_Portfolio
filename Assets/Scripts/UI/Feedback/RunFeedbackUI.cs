using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KAM.UI.Feedback
{
    public class RunFeedbackUI : MonoBehaviour
    {
        private static RunFeedbackUI _instance;

        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _bodyText;
        private Coroutine _activeRoutine;

        public static RunFeedbackUI EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject obj = new GameObject("RunFeedbackUI");
            _instance = obj.AddComponent<RunFeedbackUI>();
            DontDestroyOnLoad(obj);
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
            DontDestroyOnLoad(gameObject);
            BuildUI();
            HideImmediate();
        }

        public void ShowDefeatedRespawn(int lostSpiritFragments, string bonfireId)
        {
            string body = lostSpiritFragments > 0
                ? $"Lost {lostSpiritFragments} Spirit Fragments\nRespawning at {bonfireId}"
                : $"No Spirit Fragments lost\nRespawning at {bonfireId}";

            ShowMessage("Defeated", body, 2.4f);
        }

        public void ShowBonfireRest(string bonfireId)
        {
            ShowMessage("Rested", $"Saved at {bonfireId}\nEnemies have returned", 1.7f);
        }

        public void ShowBonfireTravel(string bonfireId)
        {
            ShowMessage("Traveling", $"Traveling to {bonfireId}", 1.4f);
        }

        public void ShowMessage(string title, string body, float duration)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _activeRoutine = StartCoroutine(ShowMessageRoutine(title, body, duration));
        }

        private IEnumerator ShowMessageRoutine(string title, string body, float duration)
        {
            _titleText.text = title;
            _bodyText.text = body;

            yield return FadeTo(1f, 0.2f);
            yield return new WaitForSecondsRealtime(duration);
            yield return FadeTo(0f, 0.45f);

            _activeRoutine = null;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0f ? 1f : elapsed / duration;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }

        private void BuildUI()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            GameObject backdrop = CreateUIObject("Backdrop", transform);
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.78f);

            GameObject content = CreateUIObject("Content", backdrop.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(720f, 260f);
            contentRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _titleText = CreateText("Title", content.transform, 54, TextAlignmentOptions.Center);
            _bodyText = CreateText("Body", content.transform, 24, TextAlignmentOptions.Center);
            _bodyText.color = new Color(0.9f, 0.86f, 0.72f, 1f);
        }

        private void HideImmediate()
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUIObject(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize * 1.8f;
            return text;
        }
    }
}
