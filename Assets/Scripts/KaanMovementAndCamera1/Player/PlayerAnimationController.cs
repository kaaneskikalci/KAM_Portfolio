using UnityEngine;
using KAM.Core.Combat;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private CombatUnit combatUnit;

    [Header("Animator Parameters")]
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveZParameter = "MoveZ";
    [SerializeField] private string groundedParameter = "IsGrounded";
    [SerializeField] private string crouchingParameter = "IsCrouching";
    [SerializeField] private string inCombatParameter = "InCombat";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string hitTriggerParameter = "Hit";
    [SerializeField] private string deathTriggerParameter = "Death";

    [Header("Tuning")]
    [SerializeField] private float movementThreshold = 0.05f;
    [SerializeField] private float speedDampTime = 0.1f;
    [SerializeField] private float directionDampTime = 0.1f;

    private int _isMovingHash;
    private int _speedHash;
    private int _moveXHash;
    private int _moveZHash;
    private int _groundedHash;
    private int _crouchingHash;
    private int _inCombatHash;
    private int _attackTriggerHash;
    private int _hitTriggerHash;
    private int _deathTriggerHash;
    private bool _isDead;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        if (combatUnit == null)
            combatUnit = GetComponentInParent<CombatUnit>();

        _isMovingHash = Animator.StringToHash(isMovingParameter);
        _speedHash = Animator.StringToHash(speedParameter);
        _moveXHash = Animator.StringToHash(moveXParameter);
        _moveZHash = Animator.StringToHash(moveZParameter);
        _groundedHash = Animator.StringToHash(groundedParameter);
        _crouchingHash = Animator.StringToHash(crouchingParameter);
        _inCombatHash = Animator.StringToHash(inCombatParameter);
        _attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
        _hitTriggerHash = Animator.StringToHash(hitTriggerParameter);
        _deathTriggerHash = Animator.StringToHash(deathTriggerParameter);
    }

    private void OnEnable()
    {
        SubscribeCombatEvents();
    }

    private void OnDisable()
    {
        UnsubscribeCombatEvents();
    }

    private void Update()
    {
        if (_isDead || animator == null || playerMovement == null)
            return;

        UpdateMovementAnimation();
    }

    private void UpdateMovementAnimation()
    {
        Vector3 worldVelocity = playerMovement.Velocity;
        float speed = worldVelocity.magnitude;
        bool isMoving = playerMovement.MovementEnabled && speed > movementThreshold;
        bool isGrounded = characterController == null || characterController.isGrounded;
        bool isCrouching = playerMovement.IsCrouching;

        SetBoolIfExists(_isMovingHash, isMoving);
        SetBoolIfExists(_groundedHash, isGrounded);
        SetBoolIfExists(_crouchingHash, isCrouching);
        SetFloatIfExists(_speedHash, speed, speedDampTime);

        if (speed <= movementThreshold)
        {
            SetFloatIfExists(_moveXHash, 0f, directionDampTime);
            SetFloatIfExists(_moveZHash, 0f, directionDampTime);
            return;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity.normalized);
        SetFloatIfExists(_moveXHash, localVelocity.x, directionDampTime);
        SetFloatIfExists(_moveZHash, localVelocity.z, directionDampTime);
    }

    private void SubscribeCombatEvents()
    {
        if (combatUnit == null)
            combatUnit = GetComponentInParent<CombatUnit>();

        if (combatUnit == null)
            return;

        combatUnit.OnEnterCombat -= HandleEnterCombat;
        combatUnit.OnExitCombat -= HandleExitCombat;
        combatUnit.OnDamageDealt -= HandleDamageDealt;
        combatUnit.OnDamageTaken -= HandleDamageTaken;
        combatUnit.OnDeath -= HandleDeath;

        combatUnit.OnEnterCombat += HandleEnterCombat;
        combatUnit.OnExitCombat += HandleExitCombat;
        combatUnit.OnDamageDealt += HandleDamageDealt;
        combatUnit.OnDamageTaken += HandleDamageTaken;
        combatUnit.OnDeath += HandleDeath;
    }

    private void UnsubscribeCombatEvents()
    {
        if (combatUnit == null)
            return;

        combatUnit.OnEnterCombat -= HandleEnterCombat;
        combatUnit.OnExitCombat -= HandleExitCombat;
        combatUnit.OnDamageDealt -= HandleDamageDealt;
        combatUnit.OnDamageTaken -= HandleDamageTaken;
        combatUnit.OnDeath -= HandleDeath;
    }

    private void HandleEnterCombat(CombatUnit unit)
    {
        _isDead = false;
        SetBoolIfExists(_inCombatHash, true);
    }

    private void HandleExitCombat(CombatUnit unit)
    {
        SetBoolIfExists(_inCombatHash, false);
    }

    private void HandleDamageDealt(CombatUnit unit, DamageResult result)
    {
        if (_isDead)
            return;

        SetTriggerIfExists(_attackTriggerHash);
    }

    private void HandleDamageTaken(CombatUnit unit, DamageResult result)
    {
        if (_isDead || result.KilledTarget || result.DamageDealt <= 0)
            return;

        SetTriggerIfExists(_hitTriggerHash);
    }

    private void HandleDeath(CombatUnit unit)
    {
        _isDead = true;
        SetTriggerIfExists(_deathTriggerHash);
    }

    private void SetBoolIfExists(int hash, bool value)
    {
        if (HasParameter(hash))
            animator.SetBool(hash, value);
    }

    private void SetFloatIfExists(int hash, float value, float dampTime)
    {
        if (HasParameter(hash))
            animator.SetFloat(hash, value, dampTime, Time.deltaTime);
    }

    private void SetTriggerIfExists(int hash)
    {
        if (HasParameter(hash))
            animator.SetTrigger(hash);
    }

    private bool HasParameter(int hash)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.nameHash == hash)
                return true;
        }

        return false;
    }
}
