using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KAM.Interaction
{
    /// <summary>
    /// Displays the interaction prompt UI when player is near an interactable.
    /// Can be configured for world-space or screen-space display.
    /// </summary>
    public class InteractionUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionDetector detector;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform promptRoot;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private TextMeshProUGUI keyText;
        [SerializeField] private Image keyBackground;

        [Header("Display Settings")]
        [SerializeField] private bool useWorldSpace = true;
        [Tooltip("World position offset from the interactable interaction point.")]
        [SerializeField] private Vector3 worldSpaceOffset = new Vector3(0, 1.5f, 0);
        [Tooltip("Final screen-space pixel offset after world position is projected. Negative Y moves the prompt lower on screen.")]
        [SerializeField] private Vector2 screenSpaceOffset = new Vector2(0f, -90f);
        [SerializeField] private float fadeSpeed = 8f;

        [Header("Styling")]
        [SerializeField] private string keyHint = "E";
        [SerializeField] private Color promptColor = Color.white;
        [SerializeField] private Color keyColor = Color.yellow;

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateUI = true;

        private Camera _mainCamera;
        private RectTransform _rectTransform;
        private bool _isVisible;
        private float _targetAlpha;
        private Transform _currentTarget;

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (detector == null)
                detector = FindFirstObjectByType<InteractionDetector>();

            if (autoCreateUI && promptText == null)
            {
                CreateUI();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
            }

            _rectTransform = GetComponent<RectTransform>();
            CachePromptRoot();
        }

        private void OnEnable()
        {
            if (detector != null)
            {
                detector.OnInteractableFound += HandleInteractableFound;
                detector.OnInteractableLost += HandleInteractableLost;
            }
        }

        private void OnDisable()
        {
            if (detector != null)
            {
                detector.OnInteractableFound -= HandleInteractableFound;
                detector.OnInteractableLost -= HandleInteractableLost;
            }
        }

        private void Update()
        {
            // Fade animation
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, _targetAlpha, Time.deltaTime * fadeSpeed);
            }

            // Position in world space
            if (_isVisible && useWorldSpace && _currentTarget != null && _mainCamera != null)
            {
                Vector3 worldPos = _currentTarget.position + worldSpaceOffset;

                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    // Convert world position to screen position
                    Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

                    // Check if in front of camera
                    if (screenPos.z > 0)
                    {
                        SetScreenSpacePosition(screenPos + (Vector3)screenSpaceOffset);
                    }
                }
                else if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                {
                    transform.position = worldPos;
                    // Billboard - face camera
                    transform.rotation = Quaternion.LookRotation(transform.position - _mainCamera.transform.position);
                }
            }
        }

        private void CachePromptRoot()
        {
            if (promptRoot != null)
                return;

            if (promptText != null && promptText.transform.parent is RectTransform promptParent && promptParent != transform)
            {
                promptRoot = promptParent;
                return;
            }

            if (keyBackground != null && keyBackground.transform.parent is RectTransform keyParent && keyParent != transform)
            {
                promptRoot = keyParent;
            }
        }

        private void SetScreenSpacePosition(Vector3 screenPosition)
        {
            if (promptRoot != null && _rectTransform != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, screenPosition, null, out Vector2 localPoint))
                {
                    promptRoot.anchoredPosition = localPoint;
                }

                return;
            }

            transform.position = screenPosition;
        }

        private void HandleInteractableFound(IInteractable interactable)
        {
            _isVisible = true;
            _targetAlpha = 1f;

            // Update prompt text
            if (promptText != null)
            {
                promptText.text = interactable.GetInteractionPrompt();
            }

            // Get target position
            _currentTarget = interactable.GetInteractionPoint();
            if (_currentTarget == null)
            {
                MonoBehaviour mb = interactable as MonoBehaviour;
                if (mb != null)
                    _currentTarget = mb.transform;
            }
        }

        private void HandleInteractableLost(IInteractable interactable)
        {
            _isVisible = false;
            _targetAlpha = 0f;
            _currentTarget = null;
        }

        /// <summary>
        /// Creates the UI elements programmatically if not assigned in inspector.
        /// </summary>
        private void CreateUI()
        {
            // Create Canvas if needed
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // Create CanvasGroup for fading
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Create container
            GameObject container = new GameObject("InteractionPrompt");
            container.transform.SetParent(transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(200, 60);
            promptRoot = containerRect;

            // Add background
            Image bg = container.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // Add horizontal layout
            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(15, 15, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Create key hint box
            GameObject keyBox = new GameObject("KeyBox");
            keyBox.transform.SetParent(container.transform, false);

            RectTransform keyBoxRect = keyBox.AddComponent<RectTransform>();
            keyBoxRect.sizeDelta = new Vector2(40, 40);

            keyBackground = keyBox.AddComponent<Image>();
            keyBackground.color = keyColor;

            // Key text
            GameObject keyTextObj = new GameObject("KeyText");
            keyTextObj.transform.SetParent(keyBox.transform, false);

            RectTransform keyTextRect = keyTextObj.AddComponent<RectTransform>();
            keyTextRect.anchorMin = Vector2.zero;
            keyTextRect.anchorMax = Vector2.one;
            keyTextRect.sizeDelta = Vector2.zero;
            keyTextRect.anchoredPosition = Vector2.zero;

            keyText = keyTextObj.AddComponent<TextMeshProUGUI>();
            keyText.text = keyHint;
            keyText.fontSize = 24;
            keyText.fontStyle = FontStyles.Bold;
            keyText.color = Color.black;
            keyText.alignment = TextAlignmentOptions.Center;

            // Prompt text
            GameObject promptObj = new GameObject("PromptText");
            promptObj.transform.SetParent(container.transform, false);

            RectTransform promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.sizeDelta = new Vector2(120, 40);

            promptText = promptObj.AddComponent<TextMeshProUGUI>();
            promptText.text = "Interact";
            promptText.fontSize = 20;
            promptText.color = promptColor;
            promptText.alignment = TextAlignmentOptions.Left;

            ContentSizeFitter fitter = container.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Debug.Log("[InteractionUI] Auto-created UI elements");
        }

        /// <summary>
        /// Manually show the UI with a custom prompt.
        /// </summary>
        public void Show(string prompt, Transform target = null)
        {
            _isVisible = true;
            _targetAlpha = 1f;
            _currentTarget = target;

            if (promptText != null)
                promptText.text = prompt;
        }

        /// <summary>
        /// Manually hide the UI.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _targetAlpha = 0f;
        }

        /// <summary>
        /// Set the key hint text (e.g., "E", "F", "Space")
        /// </summary>
        public void SetKeyHint(string key)
        {
            keyHint = key;
            if (keyText != null)
                keyText.text = key;
        }

        /// <summary>
        /// Adjust the prompt position at runtime without touching the interactable point.
        /// </summary>
        public void SetScreenSpaceOffset(Vector2 offset)
        {
            screenSpaceOffset = offset;
        }
    }
}
