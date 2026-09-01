using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace KAM.Inventory.UI
{
    /// <summary>
    /// HUD element showing Spirit Fragments (permanent currency).
    /// Displays in the corner of the screen with animation on change.
    /// </summary>
    public class SpiritFragmentsUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private TextMeshProUGUI changeText;
        [SerializeField] private Image fragmentIcon;

        [Header("Animation")]
        [SerializeField] private float countAnimationDuration = 0.5f;
        [SerializeField] private float changeTextDuration = 2f;
        [SerializeField] private float pulseScale = 1.2f;
        [SerializeField] private Color gainColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color loseColor = new Color(1f, 0.5f, 0.5f);

        [Header("Audio")]
        [SerializeField] private AudioClip gainSound;
        [SerializeField] private AudioClip loseSound;
        [SerializeField] private AudioSource audioSource;

        private InventoryManager _inventory;
        private int _displayedCount;
        private Coroutine _countAnimation;
        private Coroutine _changeAnimation;
        private Vector3 _originalScale;
        private RectTransform _pulseTarget;

        private void Start()
        {
            ConfigureVisualStability();
            _inventory = InventoryManager.Instance;

            if (_inventory != null)
            {
                _inventory.OnSpiritFragmentsChanged += OnFragmentsChanged;
                _displayedCount = _inventory.SpiritFragments;
                UpdateDisplay(_displayedCount);
            }

            // Hide change text initially
            if (changeText != null)
            {
                changeText.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnSpiritFragmentsChanged -= OnFragmentsChanged;
            }
        }

        private void OnFragmentsChanged(int oldValue, int newValue)
        {
            int change = newValue - oldValue;

            // Start count animation
            if (_countAnimation != null)
                StopCoroutine(_countAnimation);
            _countAnimation = StartCoroutine(AnimateCount(oldValue, newValue));

            // Show change text
            if (change != 0)
            {
                ShowChangeText(change);

                // Play sound
                if (change > 0 && gainSound != null && audioSource != null)
                    audioSource.PlayOneShot(gainSound);
                else if (change < 0 && loseSound != null && audioSource != null)
                    audioSource.PlayOneShot(loseSound);
            }
        }

        private IEnumerator AnimateCount(int from, int to)
        {
            float elapsed = 0f;

            // Pulse effect
            StartCoroutine(PulseEffect());

            while (elapsed < countAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / countAnimationDuration;

                // Ease out
                t = 1f - Mathf.Pow(1f - t, 3f);

                int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                UpdateDisplay(current);

                yield return null;
            }

            _displayedCount = to;
            UpdateDisplay(to);
        }

        private IEnumerator PulseEffect()
        {
            float elapsed = 0f;
            float duration = 0.2f;
            Transform pulseTransform = _pulseTarget != null ? _pulseTarget : transform;

            // Scale up
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                pulseTransform.localScale = Vector3.Lerp(_originalScale, _originalScale * pulseScale, t);
                yield return null;
            }

            // Scale down
            elapsed = 0f;
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                pulseTransform.localScale = Vector3.Lerp(_originalScale * pulseScale, _originalScale, t);
                yield return null;
            }

            pulseTransform.localScale = _originalScale;
        }

        private void ShowChangeText(int change)
        {
            if (changeText == null) return;

            if (_changeAnimation != null)
                StopCoroutine(_changeAnimation);

            _changeAnimation = StartCoroutine(AnimateChangeText(change));
        }

        private IEnumerator AnimateChangeText(int change)
        {
            changeText.gameObject.SetActive(true);

            string sign = change > 0 ? "+" : "";
            changeText.text = $"{sign}{change}";
            changeText.color = change > 0 ? gainColor : loseColor;

            // Fade in and float up
            float elapsed = 0f;
            Vector3 startPos = changeText.rectTransform.anchoredPosition;
            Vector3 endPos = startPos + new Vector3(0, 30, 0);

            while (elapsed < changeTextDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / changeTextDuration;

                // Move up
                changeText.rectTransform.anchoredPosition = Vector3.Lerp(startPos, endPos, t);

                // Fade out in second half
                if (t > 0.5f)
                {
                    float fadeT = (t - 0.5f) / 0.5f;
                    Color c = changeText.color;
                    c.a = 1f - fadeT;
                    changeText.color = c;
                }

                yield return null;
            }

            changeText.gameObject.SetActive(false);
            changeText.rectTransform.anchoredPosition = startPos;
        }

        private void UpdateDisplay(int count)
        {
            if (countText != null)
            {
                countText.text = count.ToString("N0"); // Format with thousands separator
            }
        }

        /// <summary>
        /// Force refresh the display.
        /// </summary>
        public void Refresh()
        {
            if (_inventory != null)
            {
                _displayedCount = _inventory.SpiritFragments;
                UpdateDisplay(_displayedCount);
            }
        }

        /// <summary>
        /// Create UI elements programmatically.
        /// </summary>
        [ContextMenu("Create UI Elements")]
        public void CreateUIElements()
        {
            // Background
            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

            RectTransform rect = GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 40);

            // Horizontal layout
            HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }
            layout.spacing = 8;
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Icon
            if (fragmentIcon == null)
            {
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(30, 30);

                fragmentIcon = iconObj.AddComponent<Image>();
                fragmentIcon.color = new Color(0.8f, 0.6f, 1f); // Purple-ish for spirit
            }

            // Count text
            if (countText == null)
            {
                GameObject countObj = new GameObject("Count");
                countObj.transform.SetParent(transform, false);

                RectTransform countRect = countObj.AddComponent<RectTransform>();
                countRect.sizeDelta = new Vector2(80, 30);

                countText = countObj.AddComponent<TextMeshProUGUI>();
                countText.fontSize = 24;
                countText.fontStyle = FontStyles.Bold;
                countText.alignment = TextAlignmentOptions.Left;
                countText.text = "0";
            }

            // Change text (floating)
            if (changeText == null)
            {
                GameObject changeObj = new GameObject("ChangeText");
                changeObj.transform.SetParent(transform, false);

                RectTransform changeRect = changeObj.AddComponent<RectTransform>();
                changeRect.anchorMin = new Vector2(1, 0.5f);
                changeRect.anchorMax = new Vector2(1, 0.5f);
                changeRect.pivot = new Vector2(0, 0.5f);
                changeRect.anchoredPosition = new Vector2(10, 0);
                changeRect.sizeDelta = new Vector2(60, 30);

                changeText = changeObj.AddComponent<TextMeshProUGUI>();
                changeText.fontSize = 18;
                changeText.fontStyle = FontStyles.Bold;
                changeText.alignment = TextAlignmentOptions.Left;
                changeText.gameObject.SetActive(false);
            }

            ConfigureVisualStability();
        }

        private void ConfigureVisualStability()
        {
            if (countText != null)
            {
                _pulseTarget = countText.rectTransform;
            }
            else if (fragmentIcon != null)
            {
                _pulseTarget = fragmentIcon.rectTransform;
            }
            else
            {
                _pulseTarget = transform as RectTransform;
            }

            _originalScale = _pulseTarget != null ? _pulseTarget.localScale : transform.localScale;
            EnsureChangeTextIgnoresLayout();
        }

        private void EnsureChangeTextIgnoresLayout()
        {
            if (changeText == null)
                return;

            LayoutElement layoutElement = changeText.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = changeText.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;
        }
    }
}
