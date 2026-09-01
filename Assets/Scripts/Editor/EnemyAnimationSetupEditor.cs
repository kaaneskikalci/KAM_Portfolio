using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class EnemyAnimationSetupEditor
{
    private static readonly SetupTarget ErbukeEsbuke = new SetupTarget(
        "Erbuke Esbuke",
        "Assets/Assets/Animations/erb\u00FCke&e\u015Fb\u00FCke animations/Erb\u00FCke_v2_with_animations.fbx",
        "Assets/Assets/Animations/erb\u00FCke&e\u015Fb\u00FCke animations/ErbukeEsbukeAnim.controller");

    private static readonly SetupTarget JalamuzKempir = new SetupTarget(
        "Jalamuz Kempir",
        "Assets/Assets/Animations/jalamuz Kempir animations 1/Jalamuz_Kempir_V2_with_animations.fbx",
        "Assets/Assets/Animations/jalamuz Kempir animations 1/JalamuzKempirAnim.controller");

    private static readonly SetupTarget Cak = new SetupTarget(
        "Cak",
        "Assets/Assets/Animations/\u00E7ak  animations/\u00C7ak_v2_with_animations.fbx",
        "Assets/Assets/Animations/\u00E7ak  animations/CakAnim.controller");

    private static readonly SetupTarget Aabasi = new SetupTarget(
        "Aabasi",
        "Assets/Assets/Animations/aabasi animations/new_Armature_Aabasi_v2_with_animations.fbx",
        "Assets/Assets/Animations/aabasi animations/AabasiAnim.controller");

    private static readonly SetupTarget Azar = new SetupTarget(
        "Azar",
        "Assets/Assets/Animations/Azar animations/Azar_with_animations.fbx",
        "Assets/Assets/Animations/Azar animations/AzarAnim.controller");

    private static readonly SetupTarget Bazar = new SetupTarget(
        "Bazar",
        "Assets/Assets/Animations/Bazar animations/Bazar_with_animations.fbx",
        "Assets/Assets/Animations/Bazar animations/BazarAnim.controller");

    private static readonly SetupTarget Kazar = new SetupTarget(
        "Kazar",
        "Assets/Assets/Animations/Kazar animations/Kazar_with_animations.fbx",
        "Assets/Assets/Animations/Kazar animations/KazarAnim.controller");

    [MenuItem("KAM/Animation/Log Erbuke Esbuke FBX Clips", false, 20)]
    public static void LogErbukeEsbukeFbxClips()
    {
        LogClips(ErbukeEsbuke);
    }

    [MenuItem("KAM/Animation/Setup Erbuke Esbuke Animator", false, 21)]
    public static void SetupErbukeEsbukeAnimator()
    {
        SetupAnimator(ErbukeEsbuke);
    }

    [MenuItem("KAM/Animation/Log Jalamuz Kempir FBX Clips", false, 22)]
    public static void LogJalamuzKempirFbxClips()
    {
        LogClips(JalamuzKempir);
    }

    [MenuItem("KAM/Animation/Setup Jalamuz Kempir Animator", false, 23)]
    public static void SetupJalamuzKempirAnimator()
    {
        SetupAnimator(JalamuzKempir);
    }

    [MenuItem("KAM/Animation/Log Cak FBX Clips", false, 24)]
    public static void LogCakFbxClips()
    {
        LogClips(Cak);
    }

    [MenuItem("KAM/Animation/Setup Cak Animator", false, 25)]
    public static void SetupCakAnimator()
    {
        SetupAnimator(Cak);
    }

    [MenuItem("KAM/Animation/Log Aabasi FBX Clips", false, 26)]
    public static void LogAabasiFbxClips()
    {
        LogClips(Aabasi);
    }

    [MenuItem("KAM/Animation/Setup Aabasi Animator", false, 27)]
    public static void SetupAabasiAnimator()
    {
        SetupAnimator(Aabasi);
    }

    [MenuItem("KAM/Animation/Log Azar FBX Clips", false, 28)]
    public static void LogAzarFbxClips()
    {
        LogClips(Azar);
    }

    [MenuItem("KAM/Animation/Setup Azar Animator", false, 29)]
    public static void SetupAzarAnimator()
    {
        SetupAnimator(Azar);
    }

    [MenuItem("KAM/Animation/Log Bazar FBX Clips", false, 30)]
    public static void LogBazarFbxClips()
    {
        LogClips(Bazar);
    }

    [MenuItem("KAM/Animation/Setup Bazar Animator", false, 31)]
    public static void SetupBazarAnimator()
    {
        SetupAnimator(Bazar);
    }

    [MenuItem("KAM/Animation/Log Kazar FBX Clips", false, 32)]
    public static void LogKazarFbxClips()
    {
        LogClips(Kazar);
    }

    [MenuItem("KAM/Animation/Setup Kazar Animator", false, 33)]
    public static void SetupKazarAnimator()
    {
        SetupAnimator(Kazar);
    }

    private static void LogClips(SetupTarget target)
    {
        AnimationClip[] clips = LoadClips(target);
        if (clips.Length == 0)
        {
            Debug.LogWarning($"[Enemy Animation Setup] No clips found in {target.FbxPath}");
            return;
        }

        Debug.Log($"[Enemy Animation Setup] Clips in {target.FbxPath}: {string.Join(", ", clips.Select(clip => clip.name))}");
    }

    private static void SetupAnimator(SetupTarget target)
    {
        ConfigureImporter(target);

        AnimationClip[] clips = LoadClips(target);
        if (clips.Length == 0)
        {
            Debug.LogError($"[Enemy Animation Setup] No clips found in {target.FbxPath}");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(target.ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(target.ControllerPath);

        AddBoolParameter(controller, "IsMoving");
        AddBoolParameter(controller, "InCombat");
        AddFloatParameter(controller, "Speed");
        AddTriggerParameter(controller, "Attack");
        AddTriggerParameter(controller, "Hit");
        AddTriggerParameter(controller, "Death");

        AnimationClip idleClip = FindClip(clips, "idle") ?? clips.FirstOrDefault();
        AnimationClip walkClip = FindClipContainingAny(clips, "walk", "run", "roam", "move") ?? idleClip;
        AnimationClip combatIdleClip = FindClip(clips, "combat", "idle") ?? idleClip;
        AnimationClip attackClip = FindClipContainingAny(clips, "attack", "sald") ?? idleClip;
        AnimationClip hitClip = FindClipContainingAny(clips, "hit", "damage", "hurt") ?? idleClip;
        AnimationClip deathClip = FindClipContainingAny(clips, "death", "die", "dead") ?? idleClip;

        LogFallback(target, "Walk", walkClip, idleClip);
        LogFallback(target, "CombatIdle", combatIdleClip, idleClip);
        LogFallback(target, "Attack", attackClip, idleClip);
        LogFallback(target, "Hit", hitClip, idleClip);
        LogFallback(target, "Death", deathClip, idleClip);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idle = AddState(stateMachine, "Idle", idleClip, new Vector3(240, 0, 0));
        AnimatorState walk = AddState(stateMachine, "Walk", walkClip, new Vector3(520, -80, 0));
        AnimatorState combatIdle = AddState(stateMachine, "CombatIdle", combatIdleClip, new Vector3(520, 80, 0));
        AnimatorState attack = AddState(stateMachine, "Attack", attackClip, new Vector3(820, 0, 0));
        AnimatorState hit = AddState(stateMachine, "Hit", hitClip, new Vector3(820, 140, 0));
        AnimatorState death = AddState(stateMachine, "Death", deathClip, new Vector3(820, 280, 0));

        stateMachine.defaultState = idle;

        AddBoolTransition(idle, walk, "IsMoving", true);
        AddBoolTransition(walk, idle, "IsMoving", false);
        AddBoolTransition(idle, combatIdle, "InCombat", true);
        AddBoolTransition(walk, combatIdle, "InCombat", true);
        AddBoolTransition(combatIdle, idle, "InCombat", false);

        AddAnyStateTriggerTransition(stateMachine, attack, "Attack");
        AddAnyStateTriggerTransition(stateMachine, hit, "Hit");
        AddAnyStateTriggerTransition(stateMachine, death, "Death");

        AddExitTransition(attack, combatIdle);
        AddExitTransition(hit, combatIdle);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[Enemy Animation Setup] {target.Label} controller ready. " +
            $"Idle={idleClip?.name}, Walk={walkClip?.name}, CombatIdle={combatIdleClip?.name}, " +
            $"Attack={attackClip?.name}, Hit={hitClip?.name}, Death={deathClip?.name}");
    }

    private static void ConfigureImporter(SetupTarget target)
    {
        ModelImporter importer = AssetImporter.GetAtPath(target.FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[Enemy Animation Setup] Model importer not found: {target.FbxPath}");
            return;
        }

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static AnimationClip[] LoadClips(SetupTarget target)
    {
        return AssetDatabase.LoadAllAssetsAtPath(target.FbxPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static AnimationClip FindClip(AnimationClip[] clips, params string[] requiredTerms)
    {
        return clips.FirstOrDefault(clip =>
        {
            string lowerName = clip.name.ToLowerInvariant();
            return requiredTerms.All(term => lowerName.Contains(term.ToLowerInvariant()));
        });
    }

    private static AnimationClip FindClipContainingAny(AnimationClip[] clips, params string[] terms)
    {
        return clips.FirstOrDefault(clip =>
        {
            string lowerName = clip.name.ToLowerInvariant();
            return terms.Any(term => lowerName.Contains(term.ToLowerInvariant()));
        });
    }

    private static void LogFallback(SetupTarget target, string stateName, AnimationClip selectedClip, AnimationClip fallbackClip)
    {
        if (selectedClip == null || fallbackClip == null || selectedClip != fallbackClip)
            return;

        Debug.LogWarning($"[Enemy Animation Setup] {target.Label}: {stateName} clip not found by name. Using Idle fallback.");
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState state in stateMachine.states.ToArray())
            stateMachine.RemoveState(state.state);

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines.ToArray())
            stateMachine.RemoveStateMachine(childMachine.stateMachine);

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            stateMachine.RemoveAnyStateTransition(transition);
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(name, position);
        state.motion = motion;
        return state;
    }

    private static void AddBoolParameter(AnimatorController controller, string name)
    {
        if (!controller.parameters.Any(parameter => parameter.name == name))
            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
    }

    private static void AddFloatParameter(AnimatorController controller, string name)
    {
        if (!controller.parameters.Any(parameter => parameter.name == name))
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
    }

    private static void AddTriggerParameter(AnimatorController controller, string name)
    {
        if (!controller.parameters.Any(parameter => parameter.name == name))
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool expected)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.AddCondition(expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddAnyStateTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState to, string trigger)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.85f;
        transition.duration = 0.08f;
    }

    private readonly struct SetupTarget
    {
        public SetupTarget(string label, string fbxPath, string controllerPath)
        {
            Label = label;
            FbxPath = fbxPath;
            ControllerPath = controllerPath;
        }

        public string Label { get; }
        public string FbxPath { get; }
        public string ControllerPath { get; }
    }
}
