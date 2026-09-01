using KAM.Core.Combat;
using UnityEngine;

[RequireComponent(typeof(EnemyStateMachine))]
public class EnemyAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private EnemyMovement movement;
    [SerializeField] private CombatUnit combatUnit;

    [Header("Animator Parameters")]
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string inCombatParameter = "InCombat";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private string hitTriggerParameter = "Hit";
    [SerializeField] private string deathTriggerParameter = "Death";

    [Header("Tuning")]
    [SerializeField] private float movementThreshold = 0.05f;
    [SerializeField] private float speedDampTime = 0.1f;

    private int _isMovingHash;
    private int _speedHash;
    private int _inCombatHash;
    private int _attackTriggerHash;
    private int _hitTriggerHash;
    private int _deathTriggerHash;
    private bool _isDead;

    private void Awake()
    {
        ResolveReferences();

        _isMovingHash = Animator.StringToHash(isMovingParameter);
        _speedHash = Animator.StringToHash(speedParameter);
        _inCombatHash = Animator.StringToHash(inCombatParameter);
        _attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
        _hitTriggerHash = Animator.StringToHash(hitTriggerParameter);
        _deathTriggerHash = Animator.StringToHash(deathTriggerParameter);
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (stateMachine == null)
            stateMachine = GetComponent<EnemyStateMachine>() ?? GetComponentInParent<EnemyStateMachine>();

        if (movement == null)
            movement = GetComponent<EnemyMovement>() ?? GetComponentInParent<EnemyMovement>();

        if (combatUnit == null)
            combatUnit = GetComponent<CombatUnit>() ?? GetComponentInParent<CombatUnit>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (_isDead || animator == null || movement == null)
            return;

        float speed = movement.CurrentSpeed;
        bool isMoving = movement.IsMoving && speed > movementThreshold;
        SetBoolIfExists(_isMovingHash, isMoving);
        SetFloatIfExists(_speedHash, speed, speedDampTime);

        if (stateMachine != null)
            SetBoolIfExists(_inCombatHash, stateMachine.IsInCombat);
    }

    public void PlayAttack()
    {
        if (_isDead)
            return;

        SetTriggerIfExists(_attackTriggerHash);
    }

    private void Subscribe()
    {
        ResolveReferences();

        if (stateMachine != null)
            stateMachine.OnStateChanged += HandleStateChanged;

        if (combatUnit != null)
        {
            combatUnit.OnEnterCombat += HandleEnterCombat;
            combatUnit.OnExitCombat += HandleExitCombat;
            combatUnit.OnDamageDealt += HandleDamageDealt;
            combatUnit.OnDamageTaken += HandleDamageTaken;
            combatUnit.OnDeath += HandleDeath;
        }
    }

    private void Unsubscribe()
    {
        if (stateMachine != null)
            stateMachine.OnStateChanged -= HandleStateChanged;

        if (combatUnit != null)
        {
            combatUnit.OnEnterCombat -= HandleEnterCombat;
            combatUnit.OnExitCombat -= HandleExitCombat;
            combatUnit.OnDamageDealt -= HandleDamageDealt;
            combatUnit.OnDamageTaken -= HandleDamageTaken;
            combatUnit.OnDeath -= HandleDeath;
        }
    }

    private void HandleStateChanged(EnemyStateMachine.EnemyState previousState, EnemyStateMachine.EnemyState newState)
    {
        SetBoolIfExists(_inCombatHash, newState == EnemyStateMachine.EnemyState.Combat);
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
        PlayAttack();
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
        if (animator == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash)
                return true;
        }

        return false;
    }
}
