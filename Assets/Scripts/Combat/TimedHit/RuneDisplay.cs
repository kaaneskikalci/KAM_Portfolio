using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.UI.Feedback;

namespace KAM.Combat.TimedHit
{
    /// <summary>
    /// Visual UI display for the Timed Hit (Rune) system.
    /// Shows the directional rune, timing indicator, and feedback colors.
    /// Based on GDD 1.7.5: Visual rune appears with direction player must input.
    /// </summary>
    public class RuneDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TimedHitController controller;

        [Header("UI Elements")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image runeBackground;
        [SerializeField] private Image runeDirectionIcon;
        [SerializeField] private Image timingRing;
        [SerializeField] private Image perfectZoneIndicator;
        [SerializeField] private TextMeshProUGUI directionText;
        [SerializeField] private TextMeshProUGUI feedbackText;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Image promptPanel;

        [Header("Direction Sprites")]
        [SerializeField] private Sprite upArrowSprite;
        [SerializeField] private Sprite downArrowSprite;
        [SerializeField] private Sprite leftArrowSprite;
        [SerializeField] private Sprite rightArrowSprite;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float feedbackDisplayTime = 0.5f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseIntensity = 0.1f;

        [Header("Position Settings")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0, 2f, 0);
        [SerializeField] private bool followPlayer = true;
        [SerializeField] private Transform playerTransform;

        [Header("KAM Theme")]
        [SerializeField] private bool useKamTheme = true;
        [SerializeField] private bool hideLegacyInstructionText = true;
        [SerializeField] private Vector2 themedRootSize = new Vector2(220f, 180f);
        [SerializeField] private Vector2 themedFeedbackOffset = new Vector2(0f, -78f);
        [SerializeField] private Sprite themedRuneBackgroundSprite;
        [SerializeField] private Sprite themedDirectionSprite;

        // Private state
        private Camera _mainCamera;
        private Coroutine _fadeCoroutine;
        private Coroutine _feedbackCoroutine;
        private bool _isVisible;
        private float _currentFillAmount;
        private RectTransform _themeRoot;
        private RectTransform _runtimeWidget;
        private GameObject _runtimeCanvasObject;
        private CanvasGroup _runtimeCanvasGroup;
        private RectTransform _runtimePanelRect;
        private Image _keyBorderTop;
        private Image _keyBorderBottom;
        private Image _keyBorderLeft;
        private Image _keyBorderRight;

        private static readonly Color ThemePanelColor = new Color(0.02f, 0.025f, 0.035f, 0.46f);
        private static readonly Color ThemeKeyFillColor = new Color(0.055f, 0.055f, 0.065f, 0.96f);
        private static readonly Color ThemeBorderColor = new Color(0.9f, 0.86f, 0.74f, 0.92f);
        private static readonly Color ThemeAccentColor = new Color(0.95f, 0.75f, 0.2f, 0.95f);
        private static readonly Color ThemeTimerBackColor = new Color(0.02f, 0.025f, 0.035f, 0.92f);

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // Start hidden
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            _isVisible = false;

            ApplyTheme();
            HideLegacyInstructionText();
        }

        private void Start()
        {
            if (controller == null)
                controller = FindFirstObjectByType<TimedHitController>();

            if (controller != null)
            {
                SubscribeToEvents();
            }
            else
            {
                Debug.LogWarning("[RuneDisplay] No TimedHitController found!");
            }

            // Find player if not set
            if (followPlayer && playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }
        }

        private void OnDestroy()
        {
            if (controller != null)
                UnsubscribeFromEvents();

            if (_runtimeCanvasObject != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeCanvasObject);
                else
                    DestroyImmediate(_runtimeCanvasObject);
            }
        }

        private void SubscribeToEvents()
        {
            controller.OnSequenceStarted += HandleSequenceStarted;
            controller.OnWarningStarted += HandleWarningStarted;
            controller.OnWindowOpened += HandleWindowOpened;
            controller.OnTimedHitComplete += HandleTimedHitComplete;
            controller.OnSequenceCancelled += HandleSequenceCancelled;

            Debug.Log("[RuneDisplay] Subscribed to controller events");
        }

        private void UnsubscribeFromEvents()
        {
            controller.OnSequenceStarted -= HandleSequenceStarted;
            controller.OnWarningStarted -= HandleWarningStarted;
            controller.OnWindowOpened -= HandleWindowOpened;
            controller.OnTimedHitComplete -= HandleTimedHitComplete;
            controller.OnSequenceCancelled -= HandleSequenceCancelled;
        }

        private void Update()
        {
            if (!_isVisible || controller == null)
                return;

            UpdatePosition();
            UpdateTimingRing();
            UpdatePulseEffect();
        }

        private void UpdatePosition()
        {
            if (!followPlayer || playerTransform == null || _mainCamera == null)
                return;

            Vector3 worldPos = playerTransform.position + worldOffset;
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // Only update if in front of camera
            if (screenPos.z > 0)
            {
                if (useKamTheme && _runtimePanelRect != null)
                {
                    _runtimePanelRect.position = screenPos;
                    return;
                }

                transform.position = screenPos;
            }
        }

        private void UpdateTimingRing()
        {
            if (timingRing == null || controller.CurrentState != TimedHitState.WindowActive)
                return;

            // Fill amount decreases as time runs out (creates shrinking effect)
            timingRing.fillAmount = 1f - controller.WindowProgress;

            // Update color based on perfect zone
            if (useKamTheme)
            {
                timingRing.color = controller.IsInPerfectZone
                    ? ThemeAccentColor
                    : ThemeBorderColor;
            }
            else if (controller.IsInPerfectZone && controller.Config != null)
            {
                timingRing.color = controller.Config.PerfectColor;
            }
            else if (controller.Config != null)
            {
                timingRing.color = controller.Config.ActiveColor;
            }
        }

        private void UpdatePulseEffect()
        {
            if (runeBackground == null)
                return;

            // Pulse effect when window is active
            if (controller.CurrentState == TimedHitState.WindowActive)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
                runeBackground.transform.localScale = Vector3.one * pulse;
            }
            else
            {
                runeBackground.transform.localScale = Vector3.one;
            }
        }

        #region Event Handlers

        private void HandleSequenceStarted(RuneDirection direction)
        {
            Debug.Log($"[RuneDisplay] Sequence started - Direction: {direction}");

            // IMPORTANT: Hide any previous feedback first!
            HideFeedbackImmediate();

            // Stop any running feedback coroutine
            if (_feedbackCoroutine != null)
            {
                StopCoroutine(_feedbackCoroutine);
                _feedbackCoroutine = null;
            }

            SetDirectionDisplay(direction);
            HideLegacyInstructionText();
            FadeIn();

            if (useKamTheme)
            {
                SetRuneBackgroundColor(GetWaitingThemeColor());
            }
            else if (controller.Config != null && runeBackground != null)
            {
                runeBackground.color = controller.Config.WaitingColor;
            }

            // Reset timing ring
            if (timingRing != null)
            {
                timingRing.fillAmount = 1f;
                if (useKamTheme)
                    timingRing.color = ThemeBorderColor;
                else if (controller.Config != null)
                    timingRing.color = controller.Config.ActiveColor;
            }

            // Hide perfect zone initially
            if (perfectZoneIndicator != null)
            {
                perfectZoneIndicator.gameObject.SetActive(false);
            }
        }

        private void HideFeedbackImmediate()
        {
            if (feedbackText != null)
            {
                feedbackText.text = "";
                feedbackText.gameObject.SetActive(false);
            }
        }

        private void HandleWarningStarted()
        {
            // Could add pulsing or shake effect here
            if (runeBackground != null && (controller.Config != null || useKamTheme))
            {
                StartCoroutine(FlashWarning());
            }
        }

        private void HandleWindowOpened()
        {
            Debug.Log("[RuneDisplay] Window OPENED - You can press now!");

            if (useKamTheme)
            {
                SetRuneBackgroundColor(ThemeKeyFillColor);

                if (timingRing != null)
                    timingRing.color = ThemeAccentColor;
            }
            else if (controller.Config != null)
            {
                if (runeBackground != null)
                    runeBackground.color = controller.Config.ActiveColor;

                if (timingRing != null)
                    timingRing.color = controller.Config.ActiveColor;
            }

            // Show perfect zone indicator
            if (perfectZoneIndicator != null)
            {
                if (useKamTheme)
                {
                    perfectZoneIndicator.type = Image.Type.Simple;
                    perfectZoneIndicator.color = ThemeTimerBackColor;
                }

                perfectZoneIndicator.gameObject.SetActive(true);
            }
        }

        private void HandleTimedHitComplete(TimedHitEventData eventData)
        {
            ShowFeedback(eventData);

            // Delay fade out to show feedback
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(DelayedFadeOut(feedbackDisplayTime));
        }

        private void HandleSequenceCancelled()
        {
            FadeOut();
            HideFeedback();
        }

        #endregion

        #region Visual Updates

        private void SetDirectionDisplay(RuneDirection direction)
        {
            // Update direction icon
            if (runeDirectionIcon != null)
            {
                runeDirectionIcon.sprite = GetDirectionSprite(direction);
                runeDirectionIcon.transform.rotation = Quaternion.identity; // Reset rotation

                if (useKamTheme)
                {
                    runeDirectionIcon.color = KamModalStyle.Bone;
                    runeDirectionIcon.preserveAspect = true;
                }
            }

            // Update direction text
            if (directionText != null)
            {
                directionText.text = GetDirectionKey(direction);
            }

            if (promptText != null)
            {
                promptText.text = "PRESS";
            }
        }

        private Sprite GetDirectionSprite(RuneDirection direction)
        {
            return direction switch
            {
                RuneDirection.Up => upArrowSprite,
                RuneDirection.Down => downArrowSprite,
                RuneDirection.Left => leftArrowSprite,
                RuneDirection.Right => rightArrowSprite,
                _ => upArrowSprite
            };
        }

        private string GetDirectionKey(RuneDirection direction)
        {
            // Using Q/E/Z/X keys: Q=Up, E=Right, Z=Left, X=Down
            return direction switch
            {
                RuneDirection.Up => "Q",
                RuneDirection.Down => "X",
                RuneDirection.Left => "Z",
                RuneDirection.Right => "E",
                _ => "?"
            };
        }

        private void ShowFeedback(TimedHitEventData eventData)
        {
            if (_feedbackCoroutine != null)
                StopCoroutine(_feedbackCoroutine);

            Color feedbackColor = Color.white;
            string feedbackMessage = "";

            if (useKamTheme)
            {
                switch (eventData.Result)
                {
                    case TimedHitResult.Perfect:
                        feedbackColor = KamModalStyle.Gold;
                        feedbackMessage = "PERFECT";
                        break;
                    case TimedHitResult.Success:
                        feedbackColor = KamModalStyle.Bone;
                        feedbackMessage = "HIT";
                        break;
                    case TimedHitResult.Miss:
                        feedbackColor = KamModalStyle.Red;
                        feedbackMessage = "MISS";
                        break;
                    case TimedHitResult.Late:
                        feedbackColor = KamModalStyle.Red;
                        feedbackMessage = "LATE";
                        break;
                }
            }
            else if (controller.Config != null)
            {
                switch (eventData.Result)
                {
                    case TimedHitResult.Perfect:
                        feedbackColor = controller.Config.PerfectColor;
                        feedbackMessage = "PERFECT!";
                        break;
                    case TimedHitResult.Success:
                        feedbackColor = controller.Config.SuccessColor;
                        feedbackMessage = "HIT!";
                        break;
                    case TimedHitResult.Miss:
                        feedbackColor = controller.Config.FailColor;
                        feedbackMessage = "MISS";
                        break;
                    case TimedHitResult.Late:
                        feedbackColor = controller.Config.FailColor;
                        feedbackMessage = "LATE";
                        break;
                }
            }

            if (runeBackground != null)
                SetRuneBackgroundColor(feedbackColor);

            if (feedbackText != null)
            {
                feedbackText.text = feedbackMessage;
                feedbackText.color = feedbackColor;
                feedbackText.gameObject.SetActive(true);
            }

            _feedbackCoroutine = StartCoroutine(AnimateFeedback(eventData.Result));
        }

        private void HideFeedback()
        {
            if (feedbackText != null)
                feedbackText.gameObject.SetActive(false);
        }

        #endregion

        #region Animations

        private void FadeIn()
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration));
            _isVisible = true;
        }

        private void FadeOut()
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup?.alpha ?? 1f, 0f, fadeOutDuration));
            _isVisible = false;
        }

        private IEnumerator DelayedFadeOut(float delay)
        {
            yield return new WaitForSeconds(delay);
            FadeOut();
        }

        private IEnumerator FadeCanvasGroup(float from, float to, float duration)
        {
            CanvasGroup targetGroup = useKamTheme && _runtimeCanvasGroup != null
                ? _runtimeCanvasGroup
                : canvasGroup;

            if (targetGroup == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                targetGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            targetGroup.alpha = to;
        }

        private IEnumerator FlashWarning()
        {
            if (runeBackground == null || (!useKamTheme && controller.Config == null))
                yield break;

            Color originalColor = useKamTheme ? ThemeKeyFillColor : controller.Config.WaitingColor;
            Color flashColor = useKamTheme ? new Color(0.11f, 0.105f, 0.12f, 0.98f) : controller.Config.ActiveColor;

            int flashes = 2;
            float warningTime = controller.Config != null ? controller.Config.WarningTime : 0.25f;
            float flashDuration = Mathf.Max(0.02f, warningTime / (flashes * 2));

            for (int i = 0; i < flashes; i++)
            {
                runeBackground.color = flashColor;
                yield return new WaitForSeconds(flashDuration);
                runeBackground.color = originalColor;
                yield return new WaitForSeconds(flashDuration);
            }
        }

        private IEnumerator AnimateFeedback(TimedHitResult result)
        {
            if (feedbackText == null)
                yield break;

            // Scale up animation
            Vector3 startScale = Vector3.one * 0.5f;
            Vector3 endScale = Vector3.one;

            if (result == TimedHitResult.Perfect)
            {
                endScale = Vector3.one * 1.2f; // Bigger for perfect
            }

            float duration = 0.15f;
            float elapsed = 0f;

            feedbackText.transform.localScale = startScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic

                feedbackText.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            feedbackText.transform.localScale = endScale;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually show the display with a specific direction
        /// </summary>
        public void Show(RuneDirection direction)
        {
            SetDirectionDisplay(direction);
            FadeIn();
        }

        /// <summary>
        /// Manually hide the display
        /// </summary>
        public void Hide()
        {
            FadeOut();
            HideFeedback();
        }

        /// <summary>
        /// Set the player to follow
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }

        /// <summary>
        /// Set the controller reference
        /// </summary>
        public void SetController(TimedHitController newController)
        {
            if (controller != null)
                UnsubscribeFromEvents();

            controller = newController;

            if (controller != null)
                SubscribeToEvents();
        }

        #endregion

        private void ApplyTheme()
        {
            if (!useKamTheme)
                return;

            EnsureRuntimeThemeLayout();
            HideLegacyInstructionText();

            RectTransform root = transform as RectTransform;
            if (root != null)
            {
                root.sizeDelta = themedRootSize;
            }

            if (promptPanel != null)
            {
                promptPanel.gameObject.SetActive(true);
                promptPanel.sprite = null;
                promptPanel.type = Image.Type.Simple;
                promptPanel.color = ThemePanelColor;
                promptPanel.raycastTarget = false;
                promptPanel.rectTransform.sizeDelta = new Vector2(132f, 132f);
            }

            if (runeBackground != null)
            {
                runeBackground.sprite = themedRuneBackgroundSprite;
                runeBackground.type = Image.Type.Simple;
                runeBackground.preserveAspect = true;
                runeBackground.color = themedRuneBackgroundSprite != null
                    ? Color.white
                    : ThemeKeyFillColor;
                runeBackground.raycastTarget = false;
                runeBackground.rectTransform.anchoredPosition = Vector2.zero;
                runeBackground.rectTransform.sizeDelta = new Vector2(72f, 72f);
            }

            if (runeDirectionIcon != null)
            {
                Sprite directionSprite = themedDirectionSprite != null ? themedDirectionSprite : runeDirectionIcon.sprite;
                KamModalStyle.ApplyImageStyle(runeDirectionIcon, directionSprite, KamModalStyle.Bone, true);
                runeDirectionIcon.raycastTarget = false;
                runeDirectionIcon.gameObject.SetActive(false);
            }

            if (timingRing != null)
            {
                timingRing.sprite = null;
                timingRing.color = ThemeBorderColor;
                timingRing.type = Image.Type.Filled;
                timingRing.fillMethod = Image.FillMethod.Horizontal;
                timingRing.fillOrigin = 0;
                timingRing.fillClockwise = true;
                timingRing.raycastTarget = false;
                timingRing.rectTransform.anchoredPosition = new Vector2(0f, -48f);
                timingRing.rectTransform.sizeDelta = new Vector2(72f, 4f);
            }

            if (perfectZoneIndicator != null)
            {
                perfectZoneIndicator.sprite = null;
                perfectZoneIndicator.color = ThemeTimerBackColor;
                perfectZoneIndicator.type = Image.Type.Simple;
                perfectZoneIndicator.raycastTarget = false;
                perfectZoneIndicator.rectTransform.anchoredPosition = new Vector2(0f, -48f);
                perfectZoneIndicator.rectTransform.sizeDelta = new Vector2(76f, 6f);
            }

            StyleKeyBorder(_keyBorderTop);
            StyleKeyBorder(_keyBorderBottom);
            StyleKeyBorder(_keyBorderLeft);
            StyleKeyBorder(_keyBorderRight);

            KamModalStyle.ApplyTextStyle(promptText, 18f, ThemeBorderColor, TextAlignmentOptions.Center, FontStyles.Bold);
            if (promptText != null)
            {
                promptText.text = "PRESS";
                promptText.enableAutoSizing = true;
                promptText.fontSizeMin = 13f;
                promptText.fontSizeMax = 18f;
                promptText.outlineWidth = 0.1f;
                promptText.outlineColor = Color.black;
                promptText.raycastTarget = false;
                promptText.rectTransform.anchoredPosition = new Vector2(0f, 62f);
                promptText.rectTransform.sizeDelta = new Vector2(120f, 24f);
            }

            KamModalStyle.ApplyTextStyle(directionText, 46f, KamModalStyle.Bone, TextAlignmentOptions.Center, FontStyles.Bold);
            if (directionText != null)
            {
                directionText.enableAutoSizing = false;
                directionText.outlineWidth = 0.12f;
                directionText.outlineColor = Color.black;
                directionText.raycastTarget = false;
                directionText.rectTransform.anchoredPosition = Vector2.zero;
                directionText.rectTransform.sizeDelta = new Vector2(72f, 72f);
            }

            KamModalStyle.ApplyTextStyle(feedbackText, 23f, KamModalStyle.Bone, TextAlignmentOptions.Center, FontStyles.Bold);

            if (feedbackText != null)
            {
                feedbackText.rectTransform.anchoredPosition = themedFeedbackOffset;
                feedbackText.enableAutoSizing = true;
                feedbackText.fontSizeMin = 15f;
                feedbackText.fontSizeMax = 23f;
                feedbackText.outlineWidth = 0.16f;
                feedbackText.outlineColor = Color.black;
                feedbackText.raycastTarget = false;
                feedbackText.rectTransform.sizeDelta = new Vector2(210f, 34f);
            }
        }

        private void StyleKeyBorder(Image border)
        {
            if (border == null)
                return;

            border.sprite = null;
            border.type = Image.Type.Simple;
            border.color = ThemeBorderColor;
            border.raycastTarget = false;
        }

        private void EnsureThemeLayout()
        {
            _themeRoot = transform as RectTransform;
            if (_themeRoot == null)
                return;

            if (promptPanel == null)
            {
                Transform existingPanel = transform.Find("KamTimedHitPanel");
                GameObject panelObject = existingPanel != null
                    ? existingPanel.gameObject
                    : new GameObject("KamTimedHitPanel");

                panelObject.transform.SetParent(transform, false);
                promptPanel = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();
            }

            RectTransform panelRect = promptPanel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.SetAsFirstSibling();

            if (promptText == null)
            {
                Transform existingPrompt = transform.Find("PromptText");
                GameObject promptObject = existingPrompt != null
                    ? existingPrompt.gameObject
                    : new GameObject("PromptText");

                promptObject.transform.SetParent(transform, false);
                promptText = promptObject.GetComponent<TextMeshProUGUI>() ?? promptObject.AddComponent<TextMeshProUGUI>();
            }

            RectTransform promptRect = promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);

            NormalizeThemeChild(runeBackground?.rectTransform, 1);
            NormalizeThemeChild(timingRing?.rectTransform, 2);
            NormalizeThemeChild(perfectZoneIndicator?.rectTransform, 3);
            NormalizeThemeChild(directionText?.rectTransform, 4);
            NormalizeThemeChild(feedbackText?.rectTransform, 5);
            promptText.transform.SetSiblingIndex(Mathf.Min(6, transform.childCount - 1));
        }

        private void NormalizeThemeChild(RectTransform rect, int siblingIndex)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.SetSiblingIndex(Mathf.Min(siblingIndex, transform.childCount - 1));
        }

        private void EnsureRuntimeThemeLayout()
        {
            _themeRoot = transform as RectTransform;
            HideAuthoredChildren();

            if (_runtimeCanvasObject == null)
            {
                _runtimeCanvasObject = new GameObject("TimedHitRuntimeCanvas", typeof(RectTransform));
                Canvas canvas = _runtimeCanvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 7600;

                CanvasScaler scaler = _runtimeCanvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            _runtimeCanvasGroup = _runtimeCanvasObject.GetComponent<CanvasGroup>();
            if (_runtimeCanvasGroup == null)
                _runtimeCanvasGroup = _runtimeCanvasObject.AddComponent<CanvasGroup>();

            _runtimeCanvasGroup.alpha = 0f;
            _runtimeCanvasGroup.interactable = false;
            _runtimeCanvasGroup.blocksRaycasts = false;

            Transform existingWidget = _runtimeCanvasObject.transform.Find("RuntimeTimedHitWidget");
            GameObject widgetObject = existingWidget != null
                ? existingWidget.gameObject
                : new GameObject("RuntimeTimedHitWidget", typeof(RectTransform));

            _runtimeWidget = widgetObject.GetComponent<RectTransform>();
            _runtimeWidget.SetParent(_runtimeCanvasObject.transform, false);
            _runtimeWidget.anchorMin = new Vector2(0.5f, 0.5f);
            _runtimeWidget.anchorMax = new Vector2(0.5f, 0.5f);
            _runtimeWidget.pivot = new Vector2(0.5f, 0.5f);
            _runtimeWidget.anchoredPosition = Vector2.zero;
            _runtimeWidget.sizeDelta = themedRootSize;
            _runtimeWidget.localRotation = Quaternion.identity;
            _runtimeWidget.localScale = Vector3.one;
            _runtimePanelRect = _runtimeWidget;

            widgetObject.SetActive(true);
            widgetObject.transform.SetAsLastSibling();

            promptPanel = GetOrCreateImage(_runtimeWidget, "Panel", 0, new Vector2(132f, 132f), Vector2.zero);
            perfectZoneIndicator = GetOrCreateImage(_runtimeWidget, "TimingBack", 1, new Vector2(76f, 6f), new Vector2(0f, -48f));
            runeBackground = GetOrCreateImage(_runtimeWidget, "KeyFrame", 2, new Vector2(72f, 72f), Vector2.zero);
            _keyBorderTop = GetOrCreateImage(_runtimeWidget, "KeyBorderTop", 3, new Vector2(78f, 3f), new Vector2(0f, 39f));
            _keyBorderBottom = GetOrCreateImage(_runtimeWidget, "KeyBorderBottom", 4, new Vector2(78f, 3f), new Vector2(0f, -39f));
            _keyBorderLeft = GetOrCreateImage(_runtimeWidget, "KeyBorderLeft", 5, new Vector2(3f, 78f), new Vector2(-39f, 0f));
            _keyBorderRight = GetOrCreateImage(_runtimeWidget, "KeyBorderRight", 6, new Vector2(3f, 78f), new Vector2(39f, 0f));
            directionText = GetOrCreateText(_runtimeWidget, "KeyText", 7, new Vector2(72f, 72f), Vector2.zero);
            timingRing = GetOrCreateImage(_runtimeWidget, "TimingBar", 8, new Vector2(72f, 4f), new Vector2(0f, -48f));
            promptText = GetOrCreateText(_runtimeWidget, "PromptText", 9, new Vector2(120f, 24f), new Vector2(0f, 62f));
            feedbackText = GetOrCreateText(_runtimeWidget, "FeedbackText", 10, new Vector2(210f, 34f), themedFeedbackOffset);

            if (runeDirectionIcon != null)
                runeDirectionIcon.gameObject.SetActive(false);
        }

        private void HideAuthoredChildren()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        private Image GetOrCreateImage(RectTransform parent, string name, int siblingIndex, Vector2 size, Vector2 anchoredPosition)
        {
            RectTransform rect = GetOrCreateRect(parent, name, siblingIndex, size, anchoredPosition);
            Image image = rect.GetComponent<Image>();
            if (image == null)
                image = rect.gameObject.AddComponent<Image>();

            return image;
        }

        private TextMeshProUGUI GetOrCreateText(RectTransform parent, string name, int siblingIndex, Vector2 size, Vector2 anchoredPosition)
        {
            RectTransform rect = GetOrCreateRect(parent, name, siblingIndex, size, anchoredPosition);
            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            return text;
        }

        private RectTransform GetOrCreateRect(RectTransform parent, string name, int siblingIndex, Vector2 size, Vector2 anchoredPosition)
        {
            Transform existing = parent.Find(name);
            GameObject obj = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform));

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.gameObject.SetActive(true);
            rect.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
            return rect;
        }

        private void HideLegacyInstructionText()
        {
            if (!hideLegacyInstructionText)
                return;

            Transform searchRoot = transform.root != null ? transform.root : transform;
            TextMeshProUGUI[] texts = searchRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {
                if (text == null || text == promptText || text == directionText || text == feedbackText)
                    continue;

                string value = text.text;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string normalized = value.ToUpperInvariant();
                bool isLegacyTimedHitHint =
                    normalized.Contains("PRESS I J K L") ||
                    normalized.Contains("PRESS IJKL") ||
                    normalized.Contains("PRESS Q E Z X") ||
                    normalized.Contains("PRESS Q / E / Z / X") ||
                    normalized.Contains("PRESS Q/E/Z/X");

                if (isLegacyTimedHitHint)
                    text.gameObject.SetActive(false);
            }
        }

        private Color GetWaitingThemeColor()
        {
            return new Color(KamModalStyle.HeaderFallbackColor.r, KamModalStyle.HeaderFallbackColor.g, KamModalStyle.HeaderFallbackColor.b, 0.96f);
        }

        private void SetRuneBackgroundColor(Color color)
        {
            if (runeBackground == null)
                return;

            if (useKamTheme)
            {
                runeBackground.color = ThemeKeyFillColor;
                return;
            }

            runeBackground.color = runeBackground.sprite != null
                ? new Color(color.r, color.g, color.b, Mathf.Max(0.82f, color.a))
                : color;
        }

#if UNITY_EDITOR
        [ContextMenu("Apply KAM Theme")]
        private void ApplyThemeFromContextMenu()
        {
            ApplyTheme();
        }

        /// <summary>
        /// Create default UI structure for testing
        /// </summary>
        [ContextMenu("Create Default UI Structure")]
        private void CreateDefaultUIStructure()
        {
            // Create canvas group if missing
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Create background
            if (runeBackground == null)
            {
                var bgObj = new GameObject("RuneBackground");
                bgObj.transform.SetParent(transform);
                runeBackground = bgObj.AddComponent<Image>();
                runeBackground.rectTransform.sizeDelta = new Vector2(100, 100);
            }

            // Create timing ring
            if (timingRing == null)
            {
                var ringObj = new GameObject("TimingRing");
                ringObj.transform.SetParent(transform);
                timingRing = ringObj.AddComponent<Image>();
                timingRing.type = Image.Type.Filled;
                timingRing.fillMethod = Image.FillMethod.Radial360;
                timingRing.rectTransform.sizeDelta = new Vector2(120, 120);
            }

            Debug.Log("[RuneDisplay] Default UI structure created");
        }
#endif
    }
}
