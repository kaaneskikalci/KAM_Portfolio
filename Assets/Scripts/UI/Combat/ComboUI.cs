using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.Combat;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Displays the current combo count and multiplier.
    /// Shows animated feedback when combo increases or breaks.
    /// </summary>
    public class ComboUI : MonoBehaviour
    {
        public static ComboUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject comboPanel;
        [SerializeField] private TextMeshProUGUI comboCountText;
        [SerializeField] private TextMeshProUGUI multiplierText;
        [SerializeField] private TextMeshProUGUI comboLabelText;
        [SerializeField] private Image backgroundImage;

        [Header("Animation")]
        [SerializeField] private float punchScale = 1.3f;
        [SerializeField] private float punchDuration = 0.15f;
        [SerializeField] private float shakeIntensity = 5f;
        [SerializeField] private float shakeDuration = 0.1f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float hideDelay = 2f;

        [Header("Colors")]
        [SerializeField] private Color lowComboColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color midComboColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color highComboColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private int midComboThreshold = 3;
        [SerializeField] private int highComboThreshold = 7;

        [Header("Break Effect")]
        [SerializeField] private Color breakColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private float breakShakeIntensity = 10f;

        // Runtime
        private ComboSystem _comboSystem;
        private Vector3 _originalScale;
        private Vector3 _originalPosition;
        private Coroutine _hideCoroutine;
        private Coroutine _animationCoroutine;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (comboPanel != null)
            {
                _originalScale = comboPanel.transform.localScale;
                _originalPosition = comboPanel.transform.localPosition;
                _canvasGroup = comboPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = comboPanel.AddComponent<CanvasGroup>();
            }
        }

        private void Start()
        {
            _comboSystem = ComboSystem.Instance;

            if (_comboSystem != null)
            {
                _comboSystem.OnComboChanged += HandleComboChanged;
                _comboSystem.OnComboIncreased += HandleComboIncreased;
                _comboSystem.OnComboReset += HandleComboReset;
                _comboSystem.OnNewHighScore += HandleNewHighScore;
            }

            // Start hidden
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_comboSystem != null)
            {
                _comboSystem.OnComboChanged -= HandleComboChanged;
                _comboSystem.OnComboIncreased -= HandleComboIncreased;
                _comboSystem.OnComboReset -= HandleComboReset;
                _comboSystem.OnNewHighScore -= HandleNewHighScore;
            }

            if (Instance == this)
                Instance = null;
        }

        #region Event Handlers

        private void HandleComboChanged(int newCombo)
        {
            UpdateDisplay(newCombo);
        }

        private void HandleComboIncreased(int newCombo)
        {
            SetVisible(true);
            UpdateDisplay(newCombo);
            PlayPunchAnimation();

            // Reset hide timer
            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private void HandleComboReset(int oldCombo)
        {
            if (oldCombo > 0)
            {
                PlayBreakAnimation(oldCombo);
            }
        }

        private void HandleNewHighScore(int highScore)
        {
            // Could show special effect for new high score
            Debug.Log($"[ComboUI] New high score: {highScore}!");
        }

        #endregion

        #region Display

        private void UpdateDisplay(int comboCount)
        {
            if (comboCountText != null)
            {
                comboCountText.text = comboCount.ToString();
            }

            if (multiplierText != null && _comboSystem != null)
            {
                float multiplier = _comboSystem.ComboMultiplier;
                if (multiplier > 1f)
                {
                    multiplierText.gameObject.SetActive(true);
                    multiplierText.text = $"x{multiplier:F1}";
                }
                else
                {
                    multiplierText.gameObject.SetActive(false);
                }
            }

            // Update colors based on combo level
            UpdateColors(comboCount);
        }

        private void UpdateColors(int comboCount)
        {
            Color targetColor;

            if (comboCount >= highComboThreshold)
                targetColor = highComboColor;
            else if (comboCount >= midComboThreshold)
                targetColor = midComboColor;
            else
                targetColor = lowComboColor;

            if (comboCountText != null)
                comboCountText.color = targetColor;

            if (multiplierText != null)
                multiplierText.color = targetColor;

            if (comboLabelText != null)
                comboLabelText.color = targetColor;
        }

        private void SetVisible(bool visible)
        {
            if (comboPanel == null)
                return;

            if (!comboPanel.activeSelf)
                comboPanel.SetActive(true);

            if (_canvasGroup == null)
            {
                _canvasGroup = comboPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = comboPanel.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        #endregion

        #region Animations

        private void PlayPunchAnimation()
        {
            if (!EnsureCanAnimate())
                return;

            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            _animationCoroutine = StartCoroutine(PunchCoroutine());
        }

        private System.Collections.IEnumerator PunchCoroutine()
        {
            if (comboPanel == null) yield break;

            // Punch up
            float elapsed = 0f;
            while (elapsed < punchDuration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (punchDuration / 2f);
                comboPanel.transform.localScale = Vector3.Lerp(_originalScale, _originalScale * punchScale, t);
                yield return null;
            }

            // Punch back
            elapsed = 0f;
            while (elapsed < punchDuration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (punchDuration / 2f);
                comboPanel.transform.localScale = Vector3.Lerp(_originalScale * punchScale, _originalScale, t);
                yield return null;
            }

            comboPanel.transform.localScale = _originalScale;
        }

        private void PlayBreakAnimation(int oldCombo)
        {
            if (!EnsureCanAnimate())
                return;

            SetVisible(true);

            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            _animationCoroutine = StartCoroutine(BreakCoroutine(oldCombo));
        }

        private bool EnsureCanAnimate()
        {
            if (comboPanel == null)
                return false;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (!comboPanel.activeSelf)
                comboPanel.SetActive(true);

            return isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        private System.Collections.IEnumerator BreakCoroutine(int oldCombo)
        {
            if (comboPanel == null) yield break;

            // Show break text
            if (comboCountText != null)
            {
                comboCountText.text = $"-{oldCombo}";
                comboCountText.color = breakColor;
            }

            // Shake effect
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float shakeX = Random.Range(-breakShakeIntensity, breakShakeIntensity);
                float shakeY = Random.Range(-breakShakeIntensity, breakShakeIntensity);
                comboPanel.transform.localPosition = _originalPosition + new Vector3(shakeX, shakeY, 0);
                yield return null;
            }

            comboPanel.transform.localPosition = _originalPosition;

            // Fade out
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f - (elapsed / fadeOutDuration);
                yield return null;
            }

            SetVisible(false);
        }

        private System.Collections.IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(hideDelay);

            // Fade out
            if (_canvasGroup == null && comboPanel != null)
                _canvasGroup = comboPanel.GetComponent<CanvasGroup>() ?? comboPanel.AddComponent<CanvasGroup>();

            if (_canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    _canvasGroup.alpha = 1f - (elapsed / fadeOutDuration);
                    yield return null;
                }
            }

            SetVisible(false);
        }

        #endregion

        #region Static Setup

        /// <summary>
        /// Create the combo UI if it doesn't exist.
        /// </summary>
        public static ComboUI CreateInstance()
        {
            if (Instance != null) return Instance;

            // Find a ScreenSpace Overlay canvas, or create one
            Canvas canvas = FindScreenSpaceCanvas();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("CombatUICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create combo panel
            GameObject panelObj = new GameObject("ComboUI");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.7f);
            panelRect.anchorMax = new Vector2(0.5f, 0.7f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(200, 80);

            // Background
            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);

            // Combo count
            GameObject countObj = new GameObject("ComboCount");
            countObj.transform.SetParent(panelObj.transform, false);
            RectTransform countRect = countObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.5f, 0.3f);
            countRect.anchorMax = new Vector2(0.5f, 1);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            countRect.sizeDelta = new Vector2(100, 50);
            TextMeshProUGUI countText = countObj.AddComponent<TextMeshProUGUI>();
            countText.fontSize = 48;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.Center;
            countText.text = "0";

            // Combo label
            GameObject labelObj = new GameObject("ComboLabel");
            labelObj.transform.SetParent(panelObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0);
            labelRect.anchorMax = new Vector2(0.5f, 0.3f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.sizeDelta = new Vector2(100, 20);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = 16;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.text = "COMBO";

            // Multiplier text
            GameObject multObj = new GameObject("Multiplier");
            multObj.transform.SetParent(panelObj.transform, false);
            RectTransform multRect = multObj.AddComponent<RectTransform>();
            multRect.anchorMin = new Vector2(1, 0.5f);
            multRect.anchorMax = new Vector2(1, 0.5f);
            multRect.pivot = new Vector2(0, 0.5f);
            multRect.anchoredPosition = new Vector2(10, 0);
            multRect.sizeDelta = new Vector2(60, 30);
            TextMeshProUGUI multText = multObj.AddComponent<TextMeshProUGUI>();
            multText.fontSize = 20;
            multText.fontStyle = FontStyles.Bold;
            multText.alignment = TextAlignmentOptions.Left;
            multText.text = "x1.0";

            // Add component
            ComboUI comboUI = panelObj.AddComponent<ComboUI>();
            comboUI.comboPanel = panelObj;
            comboUI.comboCountText = countText;
            comboUI.comboLabelText = labelText;
            comboUI.multiplierText = multText;
            comboUI.backgroundImage = bgImage;

            return comboUI;
        }

        private static Canvas FindScreenSpaceCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                    return c;
            }
            return null;
        }

        #endregion
    }
}
