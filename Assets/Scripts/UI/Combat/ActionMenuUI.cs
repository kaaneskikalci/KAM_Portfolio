using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using KAM.Combat.TurnSystem;
using KAM.Combat.Actions;
using KAM.Core.Combat;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Player action selection menu during combat.
    /// Displays available actions: Attack, Skills, Items, Defend.
    /// Based on GDD combat action system.
    /// </summary>
    public class ActionMenuUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private ActionButton actionButtonPrefab;

        [Header("Main Action Buttons")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button skillsButton;
        [SerializeField] private Button itemsButton;
        [SerializeField] private Button defendButton;

        [Header("Submenus")]
        [SerializeField] private GameObject skillsSubmenu;
        [SerializeField] private GameObject itemsSubmenu;
        [SerializeField] private Transform skillsContainer;
        [SerializeField] private Transform itemsContainer;

        [Header("Settings")]
        [SerializeField] private float showAnimationDuration = 0.2f;
        [SerializeField] private float hideAnimationDuration = 0.15f;
        [SerializeField] private float submenuWidth = 280f;
        [SerializeField] private float submenuGap = 10f;
        [SerializeField] private float skillButtonHeight = 72f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Events
        public event Action OnAttackSelected;
        public event Action OnDefendSelected;
        public event Action<CombatAction> OnSkillSelected;
        public event Action<int> OnItemSelected;
        public event Action OnMenuClosed;

        // Runtime
        private TurnManager _turnManager;
        private bool _isVisible = false;
        private bool _isInteractable = true;
        private List<ActionButton> _skillButtons = new List<ActionButton>();
        private List<CombatAction> _skillButtonActions = new List<CombatAction>();
        private List<ActionButton> _itemButtons = new List<ActionButton>();

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            EnsureSkillsSubmenu();
            ConfigureSkillsSubmenuLayout();
            SetupMainButtons();
            HideSubmenus();
        }

        private void Start()
        {
            _turnManager = TurnManager.Instance;

            // Start hidden
            HideImmediate();
        }

        /// <summary>
        /// Ensure skills submenu exists. Creates it dynamically if not assigned in Inspector.
        /// </summary>
        private void EnsureSkillsSubmenu()
        {
            if (skillsSubmenu != null && skillsContainer != null) return;

            if (skillsSubmenu == null)
            {
                // Create submenu panel to the left of action buttons
                GameObject submenuObj = new GameObject("SkillsSubmenu");
                submenuObj.transform.SetParent(transform, false);

                RectTransform rect = submenuObj.AddComponent<RectTransform>();
                // Position to the left of the action menu
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(1, 0.5f);
                rect.anchoredPosition = new Vector2(-10, 0);
                rect.sizeDelta = new Vector2(250, 0);

                UnityEngine.UI.Image bg = submenuObj.AddComponent<UnityEngine.UI.Image>();
                bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

                // Content size fitter to grow with content
                var fitter = submenuObj.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

                skillsSubmenu = submenuObj;

                if (debugMode)
                    Debug.Log("[ActionMenuUI] Created SkillsSubmenu dynamically");
            }

            if (skillsContainer == null)
            {
                // Create container inside submenu with vertical layout
                GameObject containerObj = new GameObject("Container");
                containerObj.transform.SetParent(skillsSubmenu.transform, false);

                RectTransform containerRect = containerObj.AddComponent<RectTransform>();
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.offsetMin = new Vector2(8, 8);
                containerRect.offsetMax = new Vector2(-8, -8);

                var vLayout = containerObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                vLayout.spacing = 6;
                vLayout.childControlWidth = true;
                vLayout.childControlHeight = false;
                vLayout.padding = new RectOffset(0, 0, 0, 0);

                skillsContainer = containerObj.transform;
            }

            skillsSubmenu.SetActive(false);
        }

        private void SetupMainButtons()
        {
            if (attackButton != null)
            {
                attackButton.onClick.AddListener(OnAttackButtonClicked);
                if (debugMode) Debug.Log("[ActionMenuUI] Attack button connected");
            }
            else if (debugMode) Debug.LogWarning("[ActionMenuUI] Attack button NOT assigned!");

            if (skillsButton != null)
            {
                skillsButton.onClick.AddListener(OnSkillsButtonClicked);
                if (debugMode) Debug.Log("[ActionMenuUI] Skills button connected");
            }
            else if (debugMode) Debug.LogWarning("[ActionMenuUI] Skills button NOT assigned!");

            if (itemsButton != null)
            {
                itemsButton.onClick.AddListener(OnItemsButtonClicked);
                if (debugMode) Debug.Log("[ActionMenuUI] Items button connected");
            }
            else if (debugMode) Debug.LogWarning("[ActionMenuUI] Items button NOT assigned!");

            if (defendButton != null)
            {
                defendButton.onClick.AddListener(OnDefendButtonClicked);
                if (debugMode) Debug.Log("[ActionMenuUI] Defend button connected");
            }
            else if (debugMode) Debug.LogWarning("[ActionMenuUI] Defend button NOT assigned!");
        }

        private void OnDestroy()
        {
            if (attackButton != null)
                attackButton.onClick.RemoveListener(OnAttackButtonClicked);
            if (skillsButton != null)
                skillsButton.onClick.RemoveListener(OnSkillsButtonClicked);
            if (itemsButton != null)
                itemsButton.onClick.RemoveListener(OnItemsButtonClicked);
            if (defendButton != null)
                defendButton.onClick.RemoveListener(OnDefendButtonClicked);
        }

        #region Button Handlers

        private void OnAttackButtonClicked()
        {
            if (!_isInteractable) return;

            if (debugMode)
                Debug.Log("[ActionMenuUI] Attack selected");

            HideSubmenus();
            OnAttackSelected?.Invoke();
        }

        private void OnSkillsButtonClicked()
        {
            if (!_isInteractable) return;

            if (debugMode)
                Debug.Log("[ActionMenuUI] Skills menu opened");

            ConfigureSkillsSubmenuLayout();
            RefreshSkillButtonInteractability();
            ToggleSubmenu(skillsSubmenu, itemsSubmenu);
        }

        private void OnItemsButtonClicked()
        {
            if (!_isInteractable) return;

            if (debugMode)
                Debug.Log("[ActionMenuUI] Items menu opened");

            ToggleSubmenu(itemsSubmenu, skillsSubmenu);
        }

        private void OnDefendButtonClicked()
        {
            if (!_isInteractable) return;

            if (debugMode)
                Debug.Log("[ActionMenuUI] Defend selected");

            HideSubmenus();
            OnDefendSelected?.Invoke();
        }

        private void ToggleSubmenu(GameObject toShow, GameObject toHide)
        {
            if (toHide != null)
                toHide.SetActive(false);

            if (toShow != null)
                toShow.SetActive(!toShow.activeSelf);
        }

        private void HideSubmenus()
        {
            if (skillsSubmenu != null)
                skillsSubmenu.SetActive(false);
            if (itemsSubmenu != null)
                itemsSubmenu.SetActive(false);
        }

        #endregion

        #region Show/Hide

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            if (menuRoot != null)
                menuRoot.SetActive(true);

            HideSubmenus();

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeIn(showAnimationDuration));
            }

            SetInteractable(true);

            if (debugMode)
                Debug.Log("[ActionMenuUI] Shown");
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;

            HideSubmenus();

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOut(hideAnimationDuration));
            }
            else if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }

            OnMenuClosed?.Invoke();

            if (debugMode)
                Debug.Log("[ActionMenuUI] Hidden");
        }

        public void HideImmediate()
        {
            _isVisible = false;

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (menuRoot != null)
                menuRoot.SetActive(false);

            HideSubmenus();
        }

        private System.Collections.IEnumerator FadeIn(float duration)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator FadeOut(float duration)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 0f;

            if (menuRoot != null)
                menuRoot.SetActive(false);
        }

        #endregion

        #region Interactability

        public void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;

            if (attackButton != null)
                attackButton.interactable = interactable;
            if (skillsButton != null)
                skillsButton.interactable = interactable;
            if (itemsButton != null)
                itemsButton.interactable = interactable;
            if (defendButton != null)
                defendButton.interactable = interactable;

            for (int i = 0; i < _skillButtons.Count; i++)
            {
                var btn = _skillButtons[i];
                if (btn != null)
                {
                    CombatAction skill = i < _skillButtonActions.Count ? _skillButtonActions[i] : null;
                    btn.SetInteractable(interactable && IsSkillUsable(skill));
                }
            }

            foreach (var btn in _itemButtons)
            {
                if (btn != null)
                    btn.SetInteractable(interactable);
            }
        }

        #endregion

        #region Skill Population

        /// <summary>
        /// Populate skills submenu with available skills
        /// </summary>
        public void PopulateSkills(List<CombatAction> skills)
        {
            ClearSkillButtons();

            if (skillsContainer == null)
            {
                EnsureSkillsSubmenu();
            }

            if (skillsContainer == null) return;

            if (debugMode)
                Debug.Log($"[ActionMenuUI] Populating {skills.Count} skills, prefab={actionButtonPrefab != null}");

            foreach (var skill in skills)
            {
                ActionButton btn;

                if (actionButtonPrefab != null)
                {
                    btn = Instantiate(actionButtonPrefab, skillsContainer);
                }
                else
                {
                    // Create button dynamically if no prefab assigned
                    btn = CreateDynamicActionButton(skillsContainer);
                }

                EnsureSkillButtonLayout(btn.gameObject);

                string apInfo = skill.ActionPointCount > 1 ? $" ({skill.ActionPointCount} AP)" : " (1 AP)";
                string cooldownInfo = skill.IsOnCooldown ? $" [CD: {skill.CurrentCooldown}]" : "";
                btn.Setup(skill.ActionName + apInfo + cooldownInfo, skill.Description, () =>
                {
                    if (_isInteractable)
                    {
                        HideSubmenus();
                        OnSkillSelected?.Invoke(skill);
                    }
                });

                btn.SetInteractable(_isInteractable && IsSkillUsable(skill));

                _skillButtons.Add(btn);
                _skillButtonActions.Add(skill);
            }
        }

        private bool IsSkillUsable(CombatAction skill)
        {
            if (skill == null || skill.IsOnCooldown)
                return false;

            var player = FindFirstObjectByType<KAM.Core.Combat.PlayerCombatUnit>();
            return player != null && player.CanPerformAction(skill.ActionPointCost, skill.ActionPointCount);
        }

        private void RefreshSkillButtonInteractability()
        {
            for (int i = 0; i < _skillButtons.Count; i++)
            {
                var btn = _skillButtons[i];
                if (btn == null) continue;

                CombatAction skill = i < _skillButtonActions.Count ? _skillButtonActions[i] : null;
                btn.SetInteractable(_isInteractable && IsSkillUsable(skill));
            }
        }

        private void ConfigureSkillsSubmenuLayout()
        {
            if (skillsSubmenu == null) return;

            float width = submenuWidth > 0f ? submenuWidth : 280f;
            float gap = submenuGap > 0f ? submenuGap : 10f;
            float buttonHeight = Mathf.Max(72f, skillButtonHeight);

            if (skillsSubmenu.transform.parent != transform)
            {
                skillsSubmenu.transform.SetParent(transform, false);
            }
            skillsSubmenu.transform.SetAsLastSibling();

            RectTransform submenuRect = skillsSubmenu.GetComponent<RectTransform>();
            if (submenuRect != null)
            {
                submenuRect.anchorMin = new Vector2(0f, 0f);
                submenuRect.anchorMax = new Vector2(0f, 1f);
                submenuRect.pivot = new Vector2(1f, 0.5f);
                submenuRect.anchoredPosition = new Vector2(-gap, 0f);
                submenuRect.sizeDelta = new Vector2(width, 0f);
                submenuRect.localScale = Vector3.one;
            }

            ContentSizeFitter fitter = skillsSubmenu.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            if (skillsContainer != null)
            {
                RectTransform containerRect = skillsContainer.GetComponent<RectTransform>();
                if (containerRect != null)
                {
                    containerRect.anchorMin = Vector2.zero;
                    containerRect.anchorMax = Vector2.one;
                    containerRect.offsetMin = new Vector2(10f, 10f);
                    containerRect.offsetMax = new Vector2(-10f, -10f);
                }

                VerticalLayoutGroup layout = skillsContainer.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.spacing = 8f;
                    layout.childControlWidth = true;
                    layout.childControlHeight = false;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                }

                for (int i = 0; i < _skillButtons.Count; i++)
                {
                    var btn = _skillButtons[i];
                    if (btn != null)
                    {
                        EnsureSkillButtonLayout(btn.gameObject);
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log($"[ActionMenuUI] Skills submenu positioned left of action menu. " +
                    $"buttons={_skillButtons.Count}, width={width}, buttonHeight={buttonHeight}");
            }
        }

        private void EnsureSkillButtonLayout(GameObject buttonObject)
        {
            if (buttonObject == null) return;

            float height = Mathf.Max(72f, skillButtonHeight);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = buttonObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleWidth = 1f;

            foreach (TextMeshProUGUI text in buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text == null)
                    continue;

                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
            }
        }

        /// <summary>
        /// Create a simple action button dynamically when no prefab is assigned.
        /// </summary>
        private ActionButton CreateDynamicActionButton(Transform parent)
        {
            GameObject btnObj = new GameObject("SkillButton");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 50);

            UnityEngine.UI.Image btnImage = btnObj.AddComponent<UnityEngine.UI.Image>();
            btnImage.color = new Color(0.18f, 0.18f, 0.28f, 0.95f);

            Button button = btnObj.AddComponent<Button>();
            button.targetGraphic = btnImage;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.18f, 0.28f, 0.95f);
            colors.highlightedColor = new Color(0.25f, 0.25f, 0.4f, 1f);
            colors.pressedColor = new Color(0.12f, 0.12f, 0.2f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            button.colors = colors;

            // Skill name text
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(btnObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(10, 0);
            nameRect.offsetMax = new Vector2(-10, -2);

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.fontSize = 16;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.alignment = TextAlignmentOptions.Left;
            nameTMP.color = new Color(1f, 0.84f, 0f); // Gold
            nameTMP.textWrappingMode = TextWrappingModes.Normal;
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;
            nameTMP.raycastTarget = false;

            // Description text
            GameObject descObj = new GameObject("DescText");
            descObj.transform.SetParent(btnObj.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(1, 0.5f);
            descRect.offsetMin = new Vector2(10, 2);
            descRect.offsetMax = new Vector2(-10, 0);

            TextMeshProUGUI descTMP = descObj.AddComponent<TextMeshProUGUI>();
            descTMP.fontSize = 11;
            descTMP.alignment = TextAlignmentOptions.Left;
            descTMP.color = new Color(0.7f, 0.7f, 0.7f);
            descTMP.textWrappingMode = TextWrappingModes.Normal;
            descTMP.overflowMode = TextOverflowModes.Ellipsis;
            descTMP.raycastTarget = false;

            // Add ActionButton component and set its fields via reflection
            ActionButton actionButton = btnObj.AddComponent<ActionButton>();
            SetPrivateField(actionButton, "button", button);
            SetPrivateField(actionButton, "nameText", nameTMP);
            SetPrivateField(actionButton, "descriptionText", descTMP);

            return actionButton;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
        }

        private void ClearSkillButtons()
        {
            foreach (var btn in _skillButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _skillButtons.Clear();
            _skillButtonActions.Clear();
        }

        #endregion

        #region Item Population

        /// <summary>
        /// Populate items submenu with available items
        /// </summary>
        public void PopulateItems(List<ItemData> items)
        {
            ClearItemButtons();

            if (itemsContainer == null || actionButtonPrefab == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var item = items[i];

                ActionButton btn = Instantiate(actionButtonPrefab, itemsContainer);
                btn.Setup(item.Name, item.Description, () =>
                {
                    if (_isInteractable)
                    {
                        HideSubmenus();
                        OnItemSelected?.Invoke(index);
                    }
                });

                _itemButtons.Add(btn);
            }
        }

        private void ClearItemButtons()
        {
            foreach (var btn in _itemButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _itemButtons.Clear();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check if the menu is currently visible
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// Set button availability based on action points
        /// </summary>
        public void UpdateAvailability(bool canAttack, bool hasSkills, bool hasItems, bool canDefend)
        {
            if (attackButton != null)
                attackButton.interactable = canAttack && _isInteractable;
            if (skillsButton != null)
                skillsButton.interactable = hasSkills && _isInteractable;
            if (itemsButton != null)
                itemsButton.interactable = hasItems && _isInteractable;
            if (defendButton != null)
                defendButton.interactable = canDefend && _isInteractable;
        }

        #endregion
    }

    /// <summary>
     /// Simple item data structure for UI display
     /// </summary>
    [Serializable]
    public struct ItemData
    {
        public string Name;
        public string Description;
        public Sprite Icon;
        public int Quantity;
    }
}
