using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.Progression;
using System;

namespace KAM.UI.SkillTree
{
    /// <summary>
    /// Visual representation of a single skill node in the skill tree UI.
    /// Displays icon, name, and visual state (Locked/Available/Unlocked).
    /// </summary>
    public class SkillNodeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image lockOverlay;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Button button;

        [Header("Colors")]
        [SerializeField] private Color lockedBorderColor = Color.gray;
        [SerializeField] private Color availableBorderColor = new Color(0.2f, 0.8f, 1f);
        [SerializeField] private Color cannotAffordBorderColor = new Color(0.5f, 0.5f, 0.7f);
        [SerializeField] private Color unlockedBorderColor = new Color(1f, 0.84f, 0f); // Gold
        [SerializeField] private Color lockedIconColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color normalIconColor = Color.white;

        private SkillNode _data;
        private NodeState _state = NodeState.Locked;

        public SkillNode Data => _data;
        public NodeState State => _state;

        public event Action<SkillNodeUI> OnClicked;

        public enum NodeState
        {
            Locked,     // Prerequisites not met
            Available,  // Prerequisites met, can potentially unlock
            Unlocked    // Already purchased
        }

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        /// <summary>
        /// Initialize this node UI with skill data.
        /// </summary>
        public void Setup(SkillNode data, Sprite iconOverride = null)
        {
            _data = data;

            if (nameText != null)
            {
                nameText.text = data.SkillName;
            }

            Sprite icon = iconOverride != null ? iconOverride : data.Icon;
            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
            }

            UpdateVisualState();
        }

        /// <summary>
        /// Refresh visual state based on current progression.
        /// </summary>
        public void UpdateVisualState()
        {
            if (_data == null || PlayerProgression.Instance == null) return;

            if (PlayerProgression.Instance.IsSkillUnlocked(_data))
            {
                SetState(NodeState.Unlocked);
            }
            else if (PlayerProgression.Instance.ArePrerequisitesMet(_data))
            {
                SetState(NodeState.Available);
            }
            else
            {
                SetState(NodeState.Locked);
            }
        }

        private void SetState(NodeState state)
        {
            _state = state;

            switch (state)
            {
                case NodeState.Locked:
                    if (borderImage != null) borderImage.color = lockedBorderColor;
                    if (iconImage != null) iconImage.color = lockedIconColor;
                    if (lockOverlay != null) lockOverlay.gameObject.SetActive(true);
                    break;

                case NodeState.Available:
                    bool canAfford = _data != null &&
                        KAM.Inventory.InventoryManager.Instance != null &&
                        KAM.Inventory.InventoryManager.Instance.CanAfford(_data.SpiritFragmentCost);

                    if (borderImage != null)
                        borderImage.color = canAfford ? availableBorderColor : cannotAffordBorderColor;
                    if (iconImage != null) iconImage.color = normalIconColor;
                    if (lockOverlay != null) lockOverlay.gameObject.SetActive(false);
                    break;

                case NodeState.Unlocked:
                    if (borderImage != null) borderImage.color = unlockedBorderColor;
                    if (iconImage != null) iconImage.color = normalIconColor;
                    if (lockOverlay != null) lockOverlay.gameObject.SetActive(false);
                    break;
            }
        }

        private void HandleClick()
        {
            OnClicked?.Invoke(this);
        }
    }
}
