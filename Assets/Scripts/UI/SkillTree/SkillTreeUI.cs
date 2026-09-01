using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KAM.Progression;
using KAM.Inventory;
using KAM.Interaction;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KAM.UI.SkillTree
{
    /// <summary>
    /// Main skill tree panel. Opened by SkillTreeAltar interaction or T key.
    /// Displays all skill nodes, handles selection and unlocking.
    /// </summary>
    public class SkillTreeUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject skillTreePanel;

        [Header("Node Layout")]
        [SerializeField] private Transform nodeContainer;
        [SerializeField] private SkillNodeUI nodePrefab;
        [SerializeField] private float nodeStartX = 125f;
        [SerializeField] private float tierSpacing = 220f;
        [SerializeField] private float nodeSpacing = 140f;
        [SerializeField] private Vector2 nodeSize = new Vector2(100f, 120f);

        [Header("Detail Panel")]
        [SerializeField] private SkillDetailPanel detailPanel;

        [Header("Currency Display")]
        [SerializeField] private TextMeshProUGUI spiritFragmentText;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.T;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        [Header("Audio")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float uiSoundVolume = 1f;

        [Header("Skill Icon Overrides")]
        [Tooltip("Optional per-skill icon assignments. Match Skill Id values like defense_rune, rune_powerup, area_damage.")]
        [SerializeField] private List<SkillIconOverride> skillIconOverrides = new List<SkillIconOverride>();

        private List<SkillNodeUI> _nodeUIs = new List<SkillNodeUI>();
        private readonly Dictionary<string, Sprite> _skillIconLookup = new Dictionary<string, Sprite>();
        private bool _isOpen;
        private SkillNodeUI _selectedNode;
        private InteractionUI _interactionUI;
        private Canvas _canvas;
        private int _originalSortingOrder;
        private GraphicRaycaster _raycaster;
        private static SkillTreeUI _openInstance;
        private static int _lastClosedFrame = -1;

#if UNITY_EDITOR
        private const string DefaultOpenSoundPath = "Assets/Assets/Sfx/ui opening.mp3";
        private const string DefaultCloseSoundPath = "Assets/Assets/Sfx/ui close.mp3";
#endif

        public bool IsOpen => _isOpen;
        public static bool IsAnyOpen => _openInstance != null && _openInstance._isOpen;
        public static bool WasClosedThisFrame => _lastClosedFrame == Time.frameCount;

        [System.Serializable]
        private class SkillIconOverride
        {
            [SerializeField] private string skillId;
            [SerializeField] private Sprite icon;

            public string SkillId => skillId;
            public Sprite Icon => icon;
        }

        public static bool CloseOpenInstance()
        {
            if (!IsAnyOpen)
                return false;

            _openInstance.Close();
            return true;
        }

        private void Start()
        {
#if UNITY_EDITOR
            AssignDefaultAudioClipsInEditor();
#endif
            EnsureAudioSource();
            RebuildSkillIconLookup();

            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();

            if (_canvas != null)
            {
                _originalSortingOrder = _canvas.sortingOrder;
            }

            // Ensure EventSystem exists (required for UI interaction)
            if (EventSystem.current == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
                Debug.LogWarning("[SkillTreeUI] No EventSystem found - created one.");
            }

            if (skillTreePanel != null)
            {
                skillTreePanel.SetActive(false);
            }
            if (detailPanel != null)
            {
                detailPanel.Hide();
                detailPanel.OnSkillChanged += RefreshAllNodes;
            }

            // Subscribe to progression events
            if (PlayerProgression.Instance != null)
            {
                PlayerProgression.Instance.OnSkillUnlocked += HandleSkillUnlocked;
            }

            // Subscribe to fragment changes
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnSpiritFragmentsChanged += HandleFragmentsChanged;
            }

            PopulateTree();

            // Disable the ScrollRect on the node container's parent. ScrollRect intercepts
            // all pointer events as potential drags, which cancels Button clicks on child nodes.
            // The tree content fits within the viewport so scrolling is unnecessary.
            if (nodeContainer != null)
            {
                ScrollRect scroll = nodeContainer.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    scroll.enabled = false;
                }
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            AssignDefaultAudioClipsInEditor();
#endif
            RebuildSkillIconLookup();

            nodeStartX = Mathf.Max(90f, nodeStartX);
            tierSpacing = Mathf.Max(80f, tierSpacing);
            nodeSpacing = Mathf.Max(80f, nodeSpacing);
            nodeSize.x = Mathf.Max(60f, nodeSize.x);
            nodeSize.y = Mathf.Max(70f, nodeSize.y);
        }

        private void OnDestroy()
        {
            if (_openInstance == this)
                _openInstance = null;

            if (PlayerProgression.Instance != null)
            {
                PlayerProgression.Instance.OnSkillUnlocked -= HandleSkillUnlocked;
            }
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnSpiritFragmentsChanged -= HandleFragmentsChanged;
            }
            if (detailPanel != null)
            {
                detailPanel.OnSkillChanged -= RefreshAllNodes;
            }
        }

        private void Update()
        {
            // DEBUG: Press F5 to add 100 Spirit Fragments for testing
            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddSpiritFragments(100);
                    Debug.Log("[SkillTreeUI DEBUG] Added 100 Spirit Fragments. Total: " + InventoryManager.Instance.SpiritFragments);
                }
            }

            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
            else if (_isOpen && Input.GetKeyDown(closeKey))
            {
                Close();
            }

            // Manual click detection: bypass Button.onClick entirely.
            // This handles clicks directly by raycasting to find which node was clicked.
            if (_isOpen && Input.GetMouseButtonDown(0))
            {
                DetectNodeClick();
            }
        }

        /// <summary>
        /// Manually raycast to find clicked skill nodes. This bypasses the Button
        /// component which may not receive events due to EventSystem/InputModule issues.
        /// </summary>
        private void DetectNodeClick()
        {
            // Try EventSystem raycast first
            if (EventSystem.current != null)
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                foreach (var result in results)
                {
                    SkillNodeUI nodeUI = result.gameObject.GetComponent<SkillNodeUI>();
                    if (nodeUI == null)
                        nodeUI = result.gameObject.GetComponentInParent<SkillNodeUI>();

                    if (nodeUI != null && _nodeUIs.Contains(nodeUI))
                    {
                        HandleNodeClicked(nodeUI);
                        return;
                    }
                }
            }

            // Fallback: manual RectTransform hit testing if EventSystem raycast fails
            Vector2 mousePos = Input.mousePosition;
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;

            foreach (var nodeUI in _nodeUIs)
            {
                RectTransform rt = nodeUI.GetComponent<RectTransform>();
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, cam))
                {
                    HandleNodeClicked(nodeUI);
                    return;
                }
            }
        }

        #region Open/Close

        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _openInstance = this;

            if (skillTreePanel != null)
            {
                skillTreePanel.SetActive(true);
            }

            // Ensure this canvas renders and raycasts above all others while open
            if (_canvas != null)
            {
                _canvas.sortingOrder = 999;
            }

            // Disable player movement while viewing skill tree
            var player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                player.SetMovementEnabled(false);
            }

            // Hide the interaction prompt by deactivating its GameObject entirely
            // (disabling only the component doesn't work because the fade lerp in Update never runs)
            _interactionUI = FindFirstObjectByType<InteractionUI>();
            if (_interactionUI != null)
            {
                _interactionUI.gameObject.SetActive(false);
            }

            RefreshAllNodes();
            UpdateSpiritFragmentDisplay();

            PlayUISound(openSound);
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _lastClosedFrame = Time.frameCount;

            if (_openInstance == this)
                _openInstance = null;

            if (skillTreePanel != null)
            {
                skillTreePanel.SetActive(false);
            }
            if (detailPanel != null)
            {
                detailPanel.Hide();
            }

            _selectedNode = null;

            // Restore original sorting order
            if (_canvas != null)
            {
                _canvas.sortingOrder = _originalSortingOrder;
            }

            // Re-enable player movement
            var player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                player.SetMovementEnabled(true);
            }

            // Re-enable interaction prompt
            if (_interactionUI != null)
            {
                _interactionUI.gameObject.SetActive(true);
            }

            PlayUISound(closeSound);
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        #endregion

        #region Tree Population

        /// <summary>
        /// Create SkillNodeUI instances for all skills in the tree.
        /// Positions nodes in a left-to-right tier layout, vertically centered within each tier.
        /// </summary>
        private void PopulateTree()
        {
            SkillTreeData treeData = null;

            if (PlayerProgression.Instance != null)
            {
                treeData = PlayerProgression.Instance.SkillTreeData;
            }

            if (treeData == null || nodeContainer == null || nodePrefab == null)
            {
                return;
            }

            // Set title
            if (titleText != null)
            {
                titleText.text = treeData.TreeName;
            }

            // Group skills by tier for layout calculation
            var tierGroups = new Dictionary<int, List<SkillNode>>();
            foreach (var skill in treeData.AllSkills)
            {
                if (skill == null) continue;
                if (!tierGroups.ContainsKey(skill.Tier))
                    tierGroups[skill.Tier] = new List<SkillNode>();
                tierGroups[skill.Tier].Add(skill);
            }

            // Create node UIs with calculated positions
            foreach (var skill in treeData.AllSkills)
            {
                if (skill == null) continue;

                SkillNodeUI nodeUI = Instantiate(nodePrefab, nodeContainer);
                nodeUI.Setup(skill, ResolveSkillIcon(skill));
                nodeUI.OnClicked += HandleNodeClicked;

                // Calculate position based on tier and index within tier
                RectTransform rt = nodeUI.GetComponent<RectTransform>();
                if (rt != null && tierGroups.ContainsKey(skill.Tier))
                {
                    // Ensure anchors are set for absolute positioning (left-center of parent)
                    // Without this, default stretch anchors (0,0→1,1) make nodes fill the
                    // entire container, overlapping each other and breaking click detection.
                    rt.anchorMin = new Vector2(0, 0.5f);
                    rt.anchorMax = new Vector2(0, 0.5f);
                    rt.sizeDelta = nodeSize;

                    var nodesInTier = tierGroups[skill.Tier];
                    int indexInTier = nodesInTier.IndexOf(skill);
                    int countInTier = nodesInTier.Count;

                    float x = nodeStartX + skill.Tier * tierSpacing;
                    // Center nodes vertically within tier, spread by nodeSpacing
                    float y = (indexInTier - (countInTier - 1) * 0.5f) * -nodeSpacing;

                    rt.anchoredPosition = new Vector2(x, y);
                }

                _nodeUIs.Add(nodeUI);
            }
        }

        #endregion

        #region Node Interaction

        private void HandleNodeClicked(SkillNodeUI nodeUI)
        {
            _selectedNode = nodeUI;

            if (detailPanel != null && nodeUI.Data != null)
            {
                detailPanel.ShowSkill(nodeUI.Data, ResolveSkillIcon(nodeUI.Data));
            }
        }

        private void RefreshAllNodes()
        {
            foreach (var nodeUI in _nodeUIs)
            {
                nodeUI.UpdateVisualState();
            }
            UpdateSpiritFragmentDisplay();

            if (_selectedNode != null && detailPanel != null && detailPanel.CurrentNode == _selectedNode.Data)
            {
                detailPanel.ShowSkill(_selectedNode.Data, ResolveSkillIcon(_selectedNode.Data));
            }
        }

        #endregion

        #region Event Handlers

        private void HandleSkillUnlocked(SkillNode node)
        {
            RefreshAllNodes();

            // Refresh detail panel if showing the unlocked skill
            if (_selectedNode != null && _selectedNode.Data == node && detailPanel != null)
            {
                detailPanel.ShowSkill(node, ResolveSkillIcon(node));
            }
        }

        private void HandleFragmentsChanged(int oldValue, int newValue)
        {
            UpdateSpiritFragmentDisplay();
            RefreshAllNodes();
        }

        private void UpdateSpiritFragmentDisplay()
        {
            if (spiritFragmentText == null) return;

            int fragments = InventoryManager.Instance != null ? InventoryManager.Instance.SpiritFragments : 0;
            spiritFragmentText.text = $"Spirit Fragments: {fragments}";
        }

        private void RebuildSkillIconLookup()
        {
            _skillIconLookup.Clear();

            if (skillIconOverrides == null)
                return;

            foreach (var entry in skillIconOverrides)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.SkillId) || entry.Icon == null)
                    continue;

                _skillIconLookup[entry.SkillId.Trim()] = entry.Icon;
            }
        }

        private Sprite ResolveSkillIcon(SkillNode skill)
        {
            if (skill == null)
                return null;

            if (!string.IsNullOrWhiteSpace(skill.SkillId) &&
                _skillIconLookup.TryGetValue(skill.SkillId.Trim(), out Sprite overrideIcon) &&
                overrideIcon != null)
            {
                return overrideIcon;
            }

            return skill.Icon;
        }

        private void PlayUISound(AudioClip clip)
        {
            if (clip == null) return;

            EnsureAudioSource();

            if (audioSource == null) return;

            audioSource.PlayOneShot(clip, uiSoundVolume);
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

#if UNITY_EDITOR
        private void AssignDefaultAudioClipsInEditor()
        {
            if (openSound == null)
            {
                openSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultOpenSoundPath);
            }

            if (closeSound == null)
            {
                closeSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultCloseSoundPath);
            }
        }
#endif

        #endregion
    }
}
