using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KAM.Core.Combat;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Individual portrait slot in the turn order bar.
    /// </summary>
    public class TurnPortraitSlot : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private TextMeshProUGUI orderNumberText;
        [SerializeField] private TextMeshProUGUI unitNameText;

        [Header("Default Sprites")]
        [SerializeField] private Sprite defaultPlayerSprite;
        [SerializeField] private Sprite defaultEnemySprite;

        private CombatUnit _unit;
        private RectTransform _rectTransform;
        private Vector3 _originalScale;
        private Color _baseBorderColor = Color.white;

        public CombatUnit Unit => _unit;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = transform.localScale;
        }

        public void Setup(CombatUnit unit, Color borderColor, bool isCurrentTurn, int orderNumber)
        {
            _unit = unit;

            if (portraitImage != null)
            {
                Sprite selectedSprite = unit.Portrait != null
                    ? unit.Portrait
                    : unit.IsPlayer ? defaultPlayerSprite : defaultEnemySprite;

                portraitImage.sprite = selectedSprite;
                portraitImage.color = selectedSprite != null ? Color.white : new Color(0.25f, 0.25f, 0.25f, 1f);
                portraitImage.preserveAspect = true;
            }

            if (borderImage != null)
            {
                borderImage.color = borderColor;
                _baseBorderColor = borderColor;
            }

            if (highlightImage != null)
                highlightImage.gameObject.SetActive(false);

            if (orderNumberText != null)
                orderNumberText.text = orderNumber.ToString();

            if (unitNameText != null)
                unitNameText.text = unit.UnitName;
        }

        public void SetScale(float scale)
        {
            transform.localScale = _originalScale * scale;
        }

        public void SetHighlight(Color color)
        {
            if (highlightImage != null)
            {
                highlightImage.gameObject.SetActive(false);
            }

            if (borderImage != null)
            {
                borderImage.color = color;
            }
        }

        public void ClearHighlight()
        {
            if (highlightImage != null)
                highlightImage.gameObject.SetActive(false);

            if (borderImage != null)
                borderImage.color = _baseBorderColor;
        }

        public void PlayTurnStartAnimation()
        {
            StopAllCoroutines();
            StartCoroutine(PulseAnimation());
        }

        private System.Collections.IEnumerator PulseAnimation()
        {
            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 pulseScale = startScale * 1.1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, pulseScale, elapsed / duration);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(pulseScale, startScale, elapsed / duration);
                yield return null;
            }

            transform.localScale = startScale;
        }

        public void SetupReferences(
            Image portrait,
            Image border,
            Image highlight,
            TextMeshProUGUI orderNumber,
            TextMeshProUGUI unitName)
        {
            portraitImage = portrait;
            borderImage = border;
            highlightImage = highlight;
            orderNumberText = orderNumber;
            unitNameText = unitName;
        }
    }
}
