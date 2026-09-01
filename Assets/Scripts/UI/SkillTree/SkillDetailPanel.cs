using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.Progression;
using KAM.Inventory;

namespace KAM.UI.SkillTree
{
    /// <summary>
    /// Side panel showing details for the selected skill node.
    /// Displays name, description, cost, prerequisites, unlock button, and branch selection.
    /// </summary>
    public class SkillDetailPanel : MonoBehaviour
    {
        [Header("Skill Info")]
        [SerializeField] private TextMeshProUGUI skillNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI prerequisitesText;
        [SerializeField] private Image skillIcon;

        [Header("Unlock")]
        [SerializeField] private Button unlockButton;
        [SerializeField] private TextMeshProUGUI unlockButtonText;
        [SerializeField] private GameObject unlockedIndicator;

        [Header("Branch Selection")]
        [SerializeField] private GameObject branchContainer;
        [SerializeField] private Button branchAButton;
        [SerializeField] private Button branchBButton;
        [SerializeField] private TextMeshProUGUI branchANameText;
        [SerializeField] private TextMeshProUGUI branchADescText;
        [SerializeField] private TextMeshProUGUI branchBNameText;
        [SerializeField] private TextMeshProUGUI branchBDescText;
        [SerializeField] private Image branchAHighlight;
        [SerializeField] private Image branchBHighlight;

        [Header("Colors")]
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color activeBranchColor = new Color(0.25f, 0.2f, 0.08f, 0.95f); // Subtle gold tint
        [SerializeField] private Color inactiveBranchColor = new Color(0.12f, 0.1f, 0.18f, 0.9f); // Default dark bg
        private static readonly Color GoldText = new Color(1f, 0.84f, 0f);

        private bool _branchLayoutFixed;

        private SkillNode _currentNode;

        /// <summary>
        /// Currently displayed skill node.
        /// </summary>
        public SkillNode CurrentNode => _currentNode;

        /// <summary>
        /// Event fired when the UI needs a refresh (after unlock or branch choice).
        /// </summary>
        public event System.Action OnSkillChanged;

        private void Awake()
        {
            if (unlockButton != null)
            {
                unlockButton.onClick.AddListener(HandleUnlock);
            }
            if (branchAButton != null)
            {
                branchAButton.onClick.AddListener(() => HandleBranchChoice(0));
            }
            if (branchBButton != null)
            {
                branchBButton.onClick.AddListener(() => HandleBranchChoice(1));
            }
        }

        private void OnDestroy()
        {
            if (unlockButton != null)
            {
                unlockButton.onClick.RemoveAllListeners();
            }
            if (branchAButton != null)
            {
                branchAButton.onClick.RemoveAllListeners();
            }
            if (branchBButton != null)
            {
                branchBButton.onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// Show details for the given skill node.
        /// </summary>
        public void ShowSkill(SkillNode node, Sprite iconOverride = null)
        {
            _currentNode = node;
            gameObject.SetActive(true);

            // Skill identity
            if (skillNameText != null) skillNameText.text = node.SkillName;
            if (descriptionText != null) descriptionText.text = node.Description;
            Sprite icon = iconOverride != null ? iconOverride : node.Icon;
            if (skillIcon != null && icon != null)
            {
                skillIcon.sprite = icon;
                skillIcon.preserveAspect = true;
            }

            // Cost
            UpdateCostDisplay(node);

            // Prerequisites
            UpdatePrerequisitesDisplay(node);

            // Unlock state
            bool isUnlocked = PlayerProgression.Instance != null && PlayerProgression.Instance.IsSkillUnlocked(node);
            bool canUnlock = PlayerProgression.Instance != null && PlayerProgression.Instance.CanUnlockSkill(node);

            if (unlockButton != null)
            {
                unlockButton.gameObject.SetActive(!isUnlocked);
                // Keep locked skills clickable so failed unlock attempts can give audio feedback.
                unlockButton.interactable = !isUnlocked;
            }
            if (unlockButtonText != null)
            {
                unlockButtonText.text = canUnlock ? "Unlock" : "Locked";
            }
            if (unlockedIndicator != null)
            {
                unlockedIndicator.SetActive(isUnlocked);
            }

            // Branch selection
            UpdateBranchDisplay(node, isUnlocked);
        }

        /// <summary>
        /// Hide the detail panel.
        /// </summary>
        public void Hide()
        {
            _currentNode = null;
            gameObject.SetActive(false);
        }

        private void UpdateCostDisplay(SkillNode node)
        {
            if (costText == null) return;

            bool canAfford = InventoryManager.Instance != null &&
                InventoryManager.Instance.CanAfford(node.SpiritFragmentCost);
            bool isUnlocked = PlayerProgression.Instance != null &&
                PlayerProgression.Instance.IsSkillUnlocked(node);

            if (isUnlocked)
            {
                costText.text = "Unlocked";
                costText.color = activeBranchColor;
            }
            else
            {
                int current = InventoryManager.Instance != null ? InventoryManager.Instance.SpiritFragments : 0;
                costText.text = $"Cost: {node.SpiritFragmentCost} Spirit Fragments ({current} available)";
                costText.color = canAfford ? affordableColor : unaffordableColor;
            }
        }

        private void UpdatePrerequisitesDisplay(SkillNode node)
        {
            if (prerequisitesText == null) return;

            if (node.Prerequisites == null || node.Prerequisites.Count == 0)
            {
                prerequisitesText.text = "No prerequisites";
                return;
            }

            string prereqText = "Requires: ";
            for (int i = 0; i < node.Prerequisites.Count; i++)
            {
                var prereq = node.Prerequisites[i];
                if (prereq == null) continue;

                bool unlocked = PlayerProgression.Instance != null &&
                    PlayerProgression.Instance.IsSkillUnlocked(prereq);
                string checkMark = unlocked ? "<color=green>✓</color>" : "<color=red>✗</color>";
                prereqText += $"{checkMark} {prereq.SkillName}";

                if (i < node.Prerequisites.Count - 1)
                {
                    prereqText += ", ";
                }
            }

            prerequisitesText.text = prereqText;
        }

        private void UpdateBranchDisplay(SkillNode node, bool isUnlocked)
        {
            bool showBranches = node.HasBranches && isUnlocked;

            if (branchContainer != null)
            {
                branchContainer.SetActive(showBranches);
            }

            if (!showBranches) return;

            // Fix branch layout once: the setup helper incorrectly set Branch A
            // anchors to (0, 1.0)→(1, 1.47) which extends above the container.
            if (!_branchLayoutFixed)
            {
                FixBranchLayout();
                _branchLayoutFixed = true;
            }

            // Branch A
            if (branchANameText != null) branchANameText.text = node.BranchA.branchName;
            if (branchADescText != null) branchADescText.text = node.BranchA.description;

            // Branch B
            if (branchBNameText != null) branchBNameText.text = node.BranchB.branchName;
            if (branchBDescText != null) branchBDescText.text = node.BranchB.description;

            // Highlight chosen branch with subtle bg tint + gold name text
            int chosenBranch = PlayerProgression.Instance != null
                ? PlayerProgression.Instance.GetChosenBranch(node.SkillId)
                : -1;

            if (branchAHighlight != null)
                branchAHighlight.color = chosenBranch == 0 ? activeBranchColor : inactiveBranchColor;
            if (branchBHighlight != null)
                branchBHighlight.color = chosenBranch == 1 ? activeBranchColor : inactiveBranchColor;

            // Color the chosen branch name gold for clearer indication
            if (branchANameText != null)
                branchANameText.color = chosenBranch == 0 ? GoldText : Color.white;
            if (branchBNameText != null)
                branchBNameText.color = chosenBranch == 1 ? GoldText : Color.white;
        }

        /// <summary>
        /// Fix branch button anchors that were incorrectly set by the setup helper.
        /// Branch A had anchorMin.y=1.0 / anchorMax.y=1.47, placing it above the container.
        /// </summary>
        private void FixBranchLayout()
        {
            if (branchAButton != null)
            {
                RectTransform rt = branchAButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0, 0.52f);
                    rt.anchorMax = new Vector2(1, 0.95f);
                    rt.offsetMin = new Vector2(0, 2);
                    rt.offsetMax = new Vector2(0, -2);
                }
            }

            if (branchBButton != null)
            {
                RectTransform rt = branchBButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0, 0.03f);
                    rt.anchorMax = new Vector2(1, 0.48f);
                    rt.offsetMin = new Vector2(0, 2);
                    rt.offsetMax = new Vector2(0, -2);
                }
            }
        }

        private void HandleUnlock()
        {
            if (_currentNode == null || PlayerProgression.Instance == null)
            {
                SkillTreeSoundManager.PlayError();
                return;
            }

            if (PlayerProgression.Instance.TryUnlockSkill(_currentNode))
            {
                SkillTreeSoundManager.PlayUnlock();

                // Refresh display
                ShowSkill(_currentNode);
                OnSkillChanged?.Invoke();
            }
            else
            {
                SkillTreeSoundManager.PlayError();
            }
        }

        private void HandleBranchChoice(int branchIndex)
        {
            if (_currentNode == null || PlayerProgression.Instance == null) return;

            if (PlayerProgression.Instance.TryChooseBranch(_currentNode, branchIndex))
            {
                // Refresh display
                ShowSkill(_currentNode);
                OnSkillChanged?.Invoke();
            }
        }
    }
}
