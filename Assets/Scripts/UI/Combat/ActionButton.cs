using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Reusable action button for combat menus.
    /// </summary>
    public class ActionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image iconImage;

        private Action _onClick;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Setup(string name, string description, Action onClick, Sprite icon = null)
        {
            if (nameText != null)
            {
                nameText.text = name;
                nameText.textWrappingMode = TextWrappingModes.Normal;
                nameText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (descriptionText != null)
            {
                descriptionText.text = description;
                descriptionText.textWrappingMode = TextWrappingModes.Normal;
                descriptionText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }

            _onClick = onClick;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private void HandleClick()
        {
            _onClick?.Invoke();
        }
    }
}
