using UnityEngine;
using TMPro;
using System.Collections;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Floating damage/heal numbers that appear in world space.
    /// Animates upward and fades out.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI damageText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float floatHeight = 1f;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float fadeStartTime = 0.5f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f);
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Random Offset")]
        [SerializeField] private float randomOffsetX = 0.5f;
        [SerializeField] private float randomOffsetY = 0.2f;

        [Header("Colors")]
        [SerializeField] private Color normalDamageColor = Color.white;
        [SerializeField] private Color criticalDamageColor = new Color(1f, 0.8f, 0f); // Gold
        [SerializeField] private Color healColor = new Color(0.2f, 1f, 0.2f); // Green
        [SerializeField] private Color missColor = new Color(0.7f, 0.7f, 0.7f); // Gray

        [Header("Sizes")]
        [SerializeField] private float normalSize = 36f;
        [SerializeField] private float criticalSize = 48f;
        [SerializeField] private float healSize = 32f;

        // Runtime
        private Vector3 _startPosition;
        private float _elapsedTime;
        private Camera _mainCamera;
        private bool _billboardToCamera = true;

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            if (_elapsedTime >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float t = _elapsedTime / lifetime;

            // Float upward
            Vector3 newPos = _startPosition + Vector3.up * (floatHeight * t);
            transform.position = newPos;

            // Scale animation
            float scale = scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;

            // Alpha animation
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alphaCurve.Evaluate(t);
            }

            // Billboard to camera
            if (_billboardToCamera && _mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }
        }

        #region Setup Methods

        /// <summary>
        /// Show normal damage number
        /// </summary>
        public void ShowDamage(int amount, bool isCritical = false)
        {
            SetupPopup(
                isCritical ? $"{amount}!" : amount.ToString(),
                isCritical ? criticalDamageColor : normalDamageColor,
                isCritical ? criticalSize : normalSize
            );

            if (isCritical)
            {
                StartCoroutine(CriticalPunchEffect());
            }
        }

        /// <summary>
        /// Show heal number
        /// </summary>
        public void ShowHeal(int amount)
        {
            SetupPopup(
                $"+{amount}",
                healColor,
                healSize
            );
        }

        /// <summary>
        /// Show miss text
        /// </summary>
        public void ShowMiss()
        {
            SetupPopup("MISS", missColor, normalSize);
        }

        /// <summary>
        /// Show custom text
        /// </summary>
        public void ShowCustom(string text, Color color, float size = -1f)
        {
            SetupPopup(text, color, size > 0 ? size : normalSize);
        }

        private void SetupPopup(string text, Color color, float size)
        {
            // Add random offset
            float offsetX = Random.Range(-randomOffsetX, randomOffsetX);
            float offsetY = Random.Range(-randomOffsetY, randomOffsetY);
            transform.position += new Vector3(offsetX, offsetY, 0f);

            _startPosition = transform.position;
            _elapsedTime = 0f;

            if (damageText != null)
            {
                damageText.text = text;
                damageText.color = color;
                damageText.fontSize = size;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private IEnumerator CriticalPunchEffect()
        {
            // Quick scale punch for critical hits
            float punchDuration = 0.15f;
            float elapsed = 0f;
            Vector3 originalScale = transform.localScale;
            Vector3 punchScale = originalScale * 1.3f;

            while (elapsed < punchDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / punchDuration;
                transform.localScale = Vector3.Lerp(punchScale, originalScale, t);
                yield return null;
            }

            transform.localScale = originalScale;
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Create a damage popup at the specified position
        /// </summary>
        public static DamagePopup Create(Vector3 position, int damage, bool isCritical = false)
        {
            var prefab = GetPrefab();
            if (prefab == null) return null;

            var popup = Instantiate(prefab, position, Quaternion.identity);
            popup.ShowDamage(damage, isCritical);
            return popup;
        }

        /// <summary>
        /// Create a heal popup at the specified position
        /// </summary>
        public static DamagePopup CreateHeal(Vector3 position, int amount)
        {
            var prefab = GetPrefab();
            if (prefab == null) return null;

            var popup = Instantiate(prefab, position, Quaternion.identity);
            popup.ShowHeal(amount);
            return popup;
        }

        /// <summary>
        /// Create a miss popup at the specified position
        /// </summary>
        public static DamagePopup CreateMiss(Vector3 position)
        {
            var prefab = GetPrefab();
            if (prefab == null) return null;

            var popup = Instantiate(prefab, position, Quaternion.identity);
            popup.ShowMiss();
            return popup;
        }

        private static DamagePopup _cachedPrefab;

        private static DamagePopup GetPrefab()
        {
            if (_cachedPrefab == null)
            {
                _cachedPrefab = Resources.Load<DamagePopup>("Prefabs/DamagePopup");

                if (_cachedPrefab == null)
                {
                    Debug.LogWarning("[DamagePopup] No prefab found at Resources/Prefabs/DamagePopup. " +
                                   "Create one and place it there, or use DamagePopupSpawner.");
                }
            }

            return _cachedPrefab;
        }

        #endregion
    }

    /// <summary>
    /// Spawner component that creates damage popups for a combat unit.
    /// Attach to units or use as a manager.
    /// </summary>
    public class DamagePopupSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DamagePopup popupPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 2f, 0f);
        [SerializeField] private Transform spawnPoint;

        private static DamagePopupSpawner _instance;
        public static DamagePopupSpawner Instance => _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Spawn a damage popup at this object's position
        /// </summary>
        public DamagePopup SpawnDamage(int damage, bool isCritical = false)
        {
            Vector3 pos = GetSpawnPosition();
            return SpawnDamageAt(pos, damage, isCritical);
        }

        /// <summary>
        /// Spawn a damage popup at a specific position
        /// </summary>
        public DamagePopup SpawnDamageAt(Vector3 position, int damage, bool isCritical = false)
        {
            if (popupPrefab == null)
            {
                Debug.LogWarning("[DamagePopupSpawner] No popup prefab assigned!");
                return null;
            }

            var popup = Instantiate(popupPrefab, position, Quaternion.identity);
            popup.ShowDamage(damage, isCritical);
            return popup;
        }

        /// <summary>
        /// Spawn a heal popup at this object's position
        /// </summary>
        public DamagePopup SpawnHeal(int amount)
        {
            Vector3 pos = GetSpawnPosition();
            return SpawnHealAt(pos, amount);
        }

        /// <summary>
        /// Spawn a heal popup at a specific position
        /// </summary>
        public DamagePopup SpawnHealAt(Vector3 position, int amount)
        {
            if (popupPrefab == null)
            {
                Debug.LogWarning("[DamagePopupSpawner] No popup prefab assigned!");
                return null;
            }

            var popup = Instantiate(popupPrefab, position, Quaternion.identity);
            popup.ShowHeal(amount);
            return popup;
        }

        /// <summary>
        /// Spawn a miss popup at this object's position
        /// </summary>
        public DamagePopup SpawnMiss()
        {
            Vector3 pos = GetSpawnPosition();

            if (popupPrefab == null)
            {
                Debug.LogWarning("[DamagePopupSpawner] No popup prefab assigned!");
                return null;
            }

            var popup = Instantiate(popupPrefab, pos, Quaternion.identity);
            popup.ShowMiss();
            return popup;
        }

        /// <summary>
        /// Spawn custom text popup
        /// </summary>
        public DamagePopup SpawnCustom(string text, Color color, float size = -1f)
        {
            Vector3 pos = GetSpawnPosition();

            if (popupPrefab == null)
            {
                Debug.LogWarning("[DamagePopupSpawner] No popup prefab assigned!");
                return null;
            }

            var popup = Instantiate(popupPrefab, pos, Quaternion.identity);
            popup.ShowCustom(text, color, size);
            return popup;
        }

        private Vector3 GetSpawnPosition()
        {
            if (spawnPoint != null)
                return spawnPoint.position;

            return transform.position + spawnOffset;
        }
    }
}
