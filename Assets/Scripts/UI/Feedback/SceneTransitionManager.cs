using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KAM.UI.Feedback
{
    public class SceneTransitionManager : MonoBehaviour
    {
        private static SceneTransitionManager _instance;
        private static bool _startupFadePending;
        private static bool _startupFadeStarted;

        [Header("Timing")]
        [SerializeField] private float fadeOutDuration = 0.65f;
        [SerializeField] private float fadeInDuration = 0.75f;
        [SerializeField] private float minimumBlackAfterLoad = 0.2f;

        [Header("Visual")]
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private int sortingOrder = 9000;

        private CanvasGroup _canvasGroup;
        private Image _fadeImage;
        private Coroutine _fadeRoutine;
        private Coroutine _loadRoutine;
        private int _blackHoldCount;

        public static SceneTransitionManager Instance => EnsureInstance();
        public static bool HasInstance => _instance != null;
        public static bool IsHoldingBlack => _instance != null && _instance._blackHoldCount > 0;
        public bool IsFullyBlack => _canvasGroup != null && _canvasGroup.alpha >= 0.99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareStartupFade()
        {
            _startupFadePending = true;
            _startupFadeStarted = false;
            EnsureInstance();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PlayStartupFade()
        {
            SceneTransitionManager manager = EnsureInstance();
            manager.StartStartupFadeIfNeeded();
        }

        public static SceneTransitionManager EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject obj = new GameObject("SceneTransitionManager");
            _instance = obj.AddComponent<SceneTransitionManager>();
            DontDestroyOnLoad(obj);
            return _instance;
        }

        public static void LoadSceneWithFade(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneTransitionManager] Cannot load scene: missing scene name.");
                return;
            }

            EnsureInstance().StartLoad(sceneName, mode);
        }

        public static void BeginBlackHold()
        {
            EnsureInstance().BeginHold();
        }

        public static void EndBlackHold(float delay = 0.15f)
        {
            if (_instance != null)
                _instance.EndHold(delay);
        }

        public static IEnumerator WaitUntilBlack()
        {
            SceneTransitionManager manager = EnsureInstance();
            while (!manager.IsFullyBlack)
                yield return null;
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
            BuildOverlay();
            SetAlpha(_startupFadePending ? 1f : 0f);
            SetBlocksRaycasts(_startupFadePending);
        }

        private void StartLoad(string sceneName, LoadSceneMode mode)
        {
            if (_loadRoutine != null)
                StopCoroutine(_loadRoutine);

            _loadRoutine = StartCoroutine(LoadSceneRoutine(sceneName, mode));
        }

        private void StartStartupFadeIfNeeded()
        {
            if (!_startupFadePending || _startupFadeStarted)
                return;

            _startupFadeStarted = true;
            StartCoroutine(StartupFadeRoutine());
        }

        private IEnumerator StartupFadeRoutine()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return TryFadeInAfterHold(minimumBlackAfterLoad);
            _startupFadePending = false;
            _startupFadeStarted = false;
        }

        private IEnumerator LoadSceneRoutine(string sceneName, LoadSceneMode mode)
        {
            yield return FadeTo(1f, fadeOutDuration);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation == null)
            {
                Debug.LogWarning($"[SceneTransitionManager] Failed to start scene load: {sceneName}");
                _loadRoutine = null;
                yield return TryFadeInAfterHold(0f);
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            yield return null;
            yield return new WaitForEndOfFrame();

            _loadRoutine = null;
            yield return TryFadeInAfterHold(minimumBlackAfterLoad);
        }

        private void BeginHold()
        {
            _blackHoldCount++;
            StartFade(1f, fadeOutDuration);
        }

        private void EndHold(float delay)
        {
            _blackHoldCount = Mathf.Max(0, _blackHoldCount - 1);

            if (_blackHoldCount == 0 && _loadRoutine == null)
                StartCoroutine(TryFadeInAfterHold(delay));
        }

        private IEnumerator TryFadeInAfterHold(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            while (_blackHoldCount > 0 || _loadRoutine != null)
                yield return null;

            yield return FadeTo(0f, fadeInDuration);
        }

        private void StartFade(float targetAlpha, float duration)
        {
            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration, trackRoutine: true));
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            yield return FadeRoutine(targetAlpha, duration, trackRoutine: false);
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, bool trackRoutine)
        {
            BuildOverlay();

            float startAlpha = _canvasGroup.alpha;
            float time = 0f;
            duration = Mathf.Max(0.01f, duration);

            SetBlocksRaycasts(true);

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, time / duration));
                yield return null;
            }

            SetAlpha(targetAlpha);
            SetBlocksRaycasts(targetAlpha > 0.001f || _blackHoldCount > 0 || _loadRoutine != null);

            if (trackRoutine)
                _fadeRoutine = null;
        }

        private void BuildOverlay()
        {
            if (_canvasGroup != null && _fadeImage != null)
                return;

            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            GraphicRaycaster raycaster = gameObject.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
                gameObject.AddComponent<GraphicRaycaster>();

            if (_fadeImage == null)
            {
                Transform existingImage = transform.Find("FadeImage");
                GameObject imageObject = existingImage != null
                    ? existingImage.gameObject
                    : new GameObject("FadeImage", typeof(RectTransform));

                imageObject.transform.SetParent(transform, false);
                _fadeImage = imageObject.GetComponent<Image>();
                if (_fadeImage == null)
                    _fadeImage = imageObject.AddComponent<Image>();
            }

            _fadeImage.color = fadeColor;
            _fadeImage.raycastTarget = true;

            RectTransform rect = _fadeImage.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        private void SetAlpha(float alpha)
        {
            BuildOverlay();
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        private void SetBlocksRaycasts(bool blocks)
        {
            BuildOverlay();
            _canvasGroup.interactable = blocks;
            _canvasGroup.blocksRaycasts = blocks;
        }
    }
}
