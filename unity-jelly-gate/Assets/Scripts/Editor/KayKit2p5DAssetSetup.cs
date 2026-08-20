using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace JellyGate.Editor
{
    // Imports the CC0 KayKit meshes as a shared Humanoid family, then creates a tiny combat
    // controller once in the editor.  Runtime only instantiates the model and flips standard
    // Animator parameters; no generated character bitmap or runtime-built cube rig is involved.
    [InitializeOnLoad]
    public static class KayKit2p5DAssetSetup
    {
        private const string Root = "Assets/Resources/KayKit2p5D";
        private const string Characters = Root + "/Characters";
        private const string Enemies = Root + "/Enemies";
        private const string ControllerPath = Root + "/KayKitBattle.controller";
        private const string AvatarSource = Characters + "/Knight.fbx";

        static KayKit2p5DAssetSetup()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Jelly Gate/Configure Authored 2.5D Characters")]
        public static void Configure()
        {
            var modelPaths = AssetDatabase.FindAssets("t:Model", new[] { Characters, Enemies })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (modelPaths.Length == 0) return;

            foreach (var path in modelPaths)
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                var changed = importer.animationType != ModelImporterAnimationType.Human;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = path == AvatarSource
                    ? ModelImporterAvatarSetup.CreateFromThisModel
                    : ModelImporterAvatarSetup.CopyFromOther;
                if (path != AvatarSource)
                    importer.sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarSource);

                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    var name = clip.name.ToLowerInvariant();
                    var loop = name.Contains("idle") || name.Contains("run") || name.Contains("walk");
                    if (clip.loopTime != loop)
                    {
                        clip.loopTime = loop;
                        changed = true;
                    }
                }
                if (clips.Length > 0) importer.clipAnimations = clips;
                if (changed) importer.SaveAndReimport();
            }

            CreateBattleController();
        }

        private static void CreateBattleController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            // The controller is authored once. Rebuilding it on each Android export creates
            // orphan state sub-assets and can multiply transitions over time.
            if (controller != null) return;
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(AvatarSource)
                .OfType<AnimationClip>()
                .Where(clip => clip != null && !clip.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (sourceClips.Length == 0)
            {
                Debug.LogWarning("KayKit model imported without animation clips; controller creation deferred.");
                return;
            }

            var idle = FindClip(sourceClips, "idle") ?? sourceClips[0];
            var run = FindClip(sourceClips, "run") ?? FindClip(sourceClips, "walk") ?? idle;
            var attack = FindClip(sourceClips, "attack", "1h") ?? FindClip(sourceClips, "attack") ?? idle;
            var cast = FindClip(sourceClips, "shoot", "1h") ?? FindClip(sourceClips, "shoot") ?? attack;
            var hurt = FindClip(sourceClips, "block") ?? FindClip(sourceClips, "hit") ?? idle;

            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hero", AnimatorControllerParameterType.Bool);

            var layer = controller.layers.Length > 0 ? controller.layers[0] : new AnimatorControllerLayer
            {
                name = "Battle",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };
            var machine = layer.stateMachine;
            machine.states = Array.Empty<ChildAnimatorState>();
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            machine.entryTransitions = Array.Empty<AnimatorTransition>();

            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            var runState = machine.AddState("Run");
            runState.motion = run;
            var attackState = machine.AddState("Attack");
            attackState.motion = attack;
            var castState = machine.AddState("Cast");
            castState.motion = cast;
            var hurtState = machine.AddState("Hurt");
            hurtState.motion = hurt;
            machine.defaultState = idleState;

            AddBoolTransition(idleState, runState, "Moving", true);
            AddBoolTransition(runState, idleState, "Moving", false);
            AddTriggerTransition(machine, attackState, "Attack");
            AddTriggerTransition(machine, castState, "Cast");
            AddTriggerTransition(machine, hurtState, "Hurt");
            AddReturnTransition(attackState, idleState);
            AddReturnTransition(castState, idleState);
            AddReturnTransition(hurtState, idleState);

            controller.layers = new[] { layer };
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip FindClip(AnimationClip[] clips, params string[] keywords)
        {
            return clips.FirstOrDefault(clip =>
            {
                var name = clip.name.ToLowerInvariant();
                return keywords.All(keyword => name.Contains(keyword));
            });
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = .11f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }

        private static void AddTriggerTransition(AnimatorStateMachine machine, AnimatorState destination, string parameter)
        {
            var transition = machine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = .04f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        private static void AddReturnTransition(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = .94f;
            transition.duration = .06f;
        }
    }
}
