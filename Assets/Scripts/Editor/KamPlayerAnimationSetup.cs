using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class KamPlayerAnimationSetup
{
    private const string FbxPath = "Assets/Assets/Animations/Kam Animation/Kam_with_animations.fbx";
    private const string ControllerPath = "Assets/Assets/Animations/CharacterAnimations/KamAnim.controller";

    private const string IsMoving = "IsMoving";
    private const string InCombat = "InCombat";
    private const string Attack = "Attack";
    private const string Hit = "Hit";
    private const string Death = "Death";

    [MenuItem("KAM/Animation/Log Kam FBX Clips", false, 10)]
    public static void LogKamFbxClips()
    {
        AnimationClip[] clips = LoadKamClips();
        if (clips.Length == 0)
        {
            Debug.LogWarning($"[Kam Animation Setup] No animation clips found in {FbxPath}.");
            return;
        }

        Debug.Log($"[Kam Animation Setup] Clips in {FbxPath}:\n" +
                  string.Join("\n", clips.Select(c => $"- {c.name}")));
    }

    [MenuItem("KAM/Animation/Setup Kam Player Animator", false, 11)]
    public static void SetupKamPlayerAnimator()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[Kam Animation Setup] Animator controller not found: {ControllerPath}");
            return;
        }

        AnimationClip[] clips = LoadKamClips();
        if (clips.Length == 0)
        {
            Debug.LogError($"[Kam Animation Setup] No animation clips found in {FbxPath}. Check the FBX Animation import settings.");
            return;
        }

        AnimationClip idleClip = FindClip(clips, "idle", exclude: "combat");
        AnimationClip combatIdleClip = FindClip(clips, "combat", "idle") ?? FindClip(clips, "combat idle") ?? idleClip;
        AnimationClip attackClip = FindClip(clips, "attack");
        AnimationClip hitClip = FindClip(clips, "hit");
        AnimationClip deathClip = FindClip(clips, "death");

        AddBoolParameter(controller, IsMoving);
        AddBoolParameter(controller, InCombat);
        AddTriggerParameter(controller, Attack);
        AddTriggerParameter(controller, Hit);
        AddTriggerParameter(controller, Death);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = FindState(stateMachine, "idle") ?? stateMachine.defaultState;
        AnimatorState walkState = FindState(stateMachine, "01-normal-walk") ?? FindState(stateMachine, "walk");

        if (idleClip != null && idleState != null)
        {
            idleState.motion = idleClip;
        }

        AnimatorState combatIdleState = EnsureState(stateMachine, "CombatIdle", combatIdleClip, new Vector3(180, 210, 0));
        AnimatorState attackState = EnsureState(stateMachine, "Attack", attackClip, new Vector3(500, 120, 0));
        AnimatorState hitState = EnsureState(stateMachine, "Hit", hitClip, new Vector3(500, 240, 0));
        AnimatorState deathState = EnsureState(stateMachine, "Death", deathClip, new Vector3(500, 360, 0));

        if (idleState != null)
        {
            EnsureBoolTransition(idleState, combatIdleState, InCombat, true, 0.1f);
            EnsureBoolTransition(combatIdleState, idleState, InCombat, false, 0.1f);
        }

        if (walkState != null)
        {
            EnsureBoolTransition(walkState, combatIdleState, InCombat, true, 0.1f);
        }

        EnsureTriggerAnyStateTransition(stateMachine, attackState, Attack, 0.05f);
        EnsureTriggerAnyStateTransition(stateMachine, hitState, Hit, 0.05f);
        EnsureTriggerAnyStateTransition(stateMachine, deathState, Death, 0.05f);

        EnsureExitTransition(attackState, combatIdleState, 0.1f);
        EnsureExitTransition(hitState, combatIdleState, 0.1f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[Kam Animation Setup] KamAnim.controller configured. " +
                  $"Idle:{ClipName(idleClip)}, CombatIdle:{ClipName(combatIdleClip)}, " +
                  $"Attack:{ClipName(attackClip)}, Hit:{ClipName(hitClip)}, Death:{ClipName(deathClip)}");
    }

    private static AnimationClip[] LoadKamClips()
    {
        return AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static AnimationClip FindClip(AnimationClip[] clips, params string[] required)
    {
        return FindClip(clips, required, exclude: null);
    }

    private static AnimationClip FindClip(AnimationClip[] clips, string required, string exclude)
    {
        return FindClip(clips, new[] { required }, exclude);
    }

    private static AnimationClip FindClip(AnimationClip[] clips, string[] required, string exclude)
    {
        return clips.FirstOrDefault(clip =>
        {
            string name = Normalize(clip.name);
            if (!string.IsNullOrEmpty(exclude) && name.Contains(Normalize(exclude)))
            {
                return false;
            }

            return required.All(part => name.Contains(Normalize(part)));
        });
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.ToLowerInvariant().Replace("_", " ").Replace("-", " ");
    }

    private static void AddBoolParameter(AnimatorController controller, string name)
    {
        if (!controller.parameters.Any(p => p.name == name))
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        }
    }

    private static void AddTriggerParameter(AnimatorController controller, string name)
    {
        if (!controller.parameters.Any(p => p.name == name))
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        return stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state != null &&
                                     string.Equals(state.name, name, System.StringComparison.OrdinalIgnoreCase));
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string name,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state = FindState(stateMachine, name);
        if (state == null)
        {
            state = stateMachine.AddState(name, position);
        }

        if (motion != null)
        {
            state.motion = motion;
        }

        return state;
    }

    private static void EnsureBoolTransition(
        AnimatorState from,
        AnimatorState to,
        string parameter,
        bool expected,
        float duration)
    {
        if (from == null || to == null || HasTransition(from, to, parameter))
        {
            return;
        }

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void EnsureTriggerAnyStateTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState to,
        string trigger,
        float duration)
    {
        if (stateMachine == null || to == null)
        {
            return;
        }

        if (stateMachine.anyStateTransitions.Any(t => t.destinationState == to &&
                                                      t.conditions.Any(c => c.parameter == trigger)))
        {
            return;
        }

        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void EnsureExitTransition(AnimatorState from, AnimatorState to, float duration)
    {
        if (from == null || to == null || from.transitions.Any(t => t.destinationState == to && t.hasExitTime))
        {
            return;
        }

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.9f;
        transition.duration = duration;
    }

    private static bool HasTransition(AnimatorState from, AnimatorState to, string parameter)
    {
        return from.transitions.Any(t => t.destinationState == to &&
                                         t.conditions.Any(c => c.parameter == parameter));
    }

    private static string ClipName(AnimationClip clip)
    {
        return clip != null ? clip.name : "missing";
    }
}
