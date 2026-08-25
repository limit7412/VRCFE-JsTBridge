using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDKBase;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// AnimatorControllerから、解析にかけるレイヤーの要約を作る
    /// </summary>
    internal static class FxLayerSnapshotReader
    {
        private const string BlendShapePrefix = "blendShape.";

        /// <summary>
        /// レイヤーごとの要約を作る
        /// Override Controllerが差し替えたクリップは、差し替え後を読む
        /// </summary>
        public static IReadOnlyList<FxLayerSnapshot> Read(RuntimeAnimatorController runtimeController)
        {
            var controller = AnimatorControllerResolver.Resolve(runtimeController);
            if (controller == null)
            {
                return new FxLayerSnapshot[0];
            }

            var overrides = AnimatorControllerResolver.CollectOverrides(runtimeController);
            var layers = controller.layers;
            var snapshots = new List<FxLayerSnapshot>(layers.Length);

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var stateMachine = ResolveStateMachine(layers, i, out var isSynced);

                snapshots.Add(new FxLayerSnapshot(
                    layer.name,
                    i,
                    CollectBlendShapeBindings(layer, stateMachine, isSynced, overrides, string.Empty),
                    ChangesTrackingControl(CollectBehaviours(layer, stateMachine, isSynced)),
                    HasWriteDefaults(stateMachine)));
            }

            return snapshots;
        }

        /// <summary>
        /// コントローラ全体が書くブレンドシェイプ
        /// FaceEmoとJerryのコントローラから、比較対象を作るために使う
        /// </summary>
        /// <remarks>
        /// Override Controllerが差し替えたクリップは、差し替え後を読む。
        /// basePathにはマージ時の前置パスを渡す。
        /// 素体FXの束縛はアバタールートからのパスなので、揃えないと突き合わせられない。
        /// </remarks>
        public static IReadOnlyCollection<string> CollectBlendShapeBindings(
            RuntimeAnimatorController runtimeController, string basePath = "")
        {
            var controller = AnimatorControllerResolver.Resolve(runtimeController);
            if (controller == null)
            {
                return new string[0];
            }

            var overrides = AnimatorControllerResolver.CollectOverrides(runtimeController);
            var layers = controller.layers;
            var bindings = new HashSet<string>();

            for (var i = 0; i < layers.Length; i++)
            {
                var stateMachine = ResolveStateMachine(layers, i, out var isSynced);
                foreach (var binding in
                    CollectBlendShapeBindings(layers[i], stateMachine, isSynced, overrides, basePath))
                {
                    bindings.Add(binding);
                }
            }

            return bindings;
        }

        /// <summary>
        /// レイヤーが実際に再生するステートマシン
        /// 同期レイヤー (Sync) は自分のステートマシンを再生しないため、同期元をたどる
        /// </summary>
        /// <remarks>
        /// 同期元がさらに別のレイヤーを同期していることもあるため、終端までたどる。
        /// 循環していれば、たどるのをやめてその手前を使う。
        /// Unityは循環した構成を書き戻しの時点で弾くため、この備えは壊れたアセット向けである。
        /// </remarks>
        private static AnimatorStateMachine ResolveStateMachine(
            AnimatorControllerLayer[] layers, int index, out bool isSynced)
        {
            var visited = new HashSet<int> { index };
            var current = index;

            while (true)
            {
                var next = layers[current].syncedLayerIndex;
                if (next < 0 || next >= layers.Length || !visited.Add(next))
                {
                    break;
                }

                current = next;
            }

            isSynced = current != index;

            return layers[current].stateMachine;
        }

        private static IReadOnlyCollection<string> CollectBlendShapeBindings(
            AnimatorControllerLayer layer,
            AnimatorStateMachine stateMachine,
            bool isSynced,
            IReadOnlyDictionary<AnimationClip, AnimationClip> overrides,
            string basePath)
        {
            var bindings = new HashSet<string>();

            foreach (var state in AnimatorGraphWalker.States(stateMachine))
            {
                // 同期レイヤーはステートごとにモーションを差し替えられる
                var motion = isSynced ? layer.GetOverrideMotion(state) ?? state.motion : state.motion;

                foreach (var clip in AnimatorGraphWalker.Clips(motion))
                {
                    AddBlendShapeBindings(
                        AnimatorControllerResolver.Apply(overrides, clip), basePath, bindings);
                }
            }

            return bindings;
        }

        /// <summary>
        /// レイヤーが動かすbehaviour
        /// </summary>
        /// <remarks>
        /// 同期レイヤーはステートごとにbehaviourを差し替えられる。
        /// 差し替えたステートでは同期元のbehaviourは動かないため、モーションと同じ規則で解決する。
        /// ステートマシン自身が持つbehaviourには差し替えがないので、そのまま数える。
        /// </remarks>
        private static IEnumerable<StateMachineBehaviour> CollectBehaviours(
            AnimatorControllerLayer layer, AnimatorStateMachine stateMachine, bool isSynced)
        {
            foreach (var behaviour in AnimatorGraphWalker.StateMachineBehaviours(stateMachine))
            {
                yield return behaviour;
            }

            foreach (var state in AnimatorGraphWalker.States(stateMachine))
            {
                var overrides = isSynced ? layer.GetOverrideBehaviours(state) : null;
                var behaviours = overrides != null && overrides.Length > 0 ? overrides : state.behaviours;

                foreach (var behaviour in behaviours)
                {
                    yield return behaviour;
                }
            }
        }

        private static void AddBlendShapeBindings(
            AnimationClip clip, string basePath, HashSet<string> bindings)
        {
            if (clip == null)
            {
                return;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                // 型も見る。同じ名前のプロパティを持つ別のコンポーネントと区別できない
                if (binding.type != typeof(SkinnedMeshRenderer)
                    || !binding.propertyName.StartsWith(BlendShapePrefix))
                {
                    continue;
                }

                bindings.Add(Combine(basePath, binding.path) + "/" + binding.propertyName);
            }
        }

        private static string Combine(string basePath, string path)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                return path;
            }

            return string.IsNullOrEmpty(path) ? basePath : basePath + "/" + path;
        }

        /// <summary>
        /// Write Defaultsが有効なステートを持つか
        /// </summary>
        /// <remarks>
        /// 有効なステートは、クリップに書いていないプロパティも既定値へ戻す。
        /// 戻す先はAnimator全体でアニメーションされるプロパティなので、
        /// マージ後に加わるFaceEmoやJerryのブレンドシェイプも含まれる。
        /// クリップを読むだけでは何を戻すか分からないため、その旨を伝えるためだけに使う。
        /// </remarks>
        private static bool HasWriteDefaults(AnimatorStateMachine stateMachine)
        {
            return AnimatorGraphWalker.States(stateMachine).Any(state => state.writeDefaultValues);
        }

        /// <summary>
        /// EyesかMouthのTracking Controlを切り替えるか
        /// 切り替えるレイヤーは、ブリッジの再適用と競合する
        /// </summary>
        private static bool ChangesTrackingControl(IEnumerable<StateMachineBehaviour> behaviours)
        {
            return behaviours
                .OfType<VRC_AnimatorTrackingControl>()
                .Any(control =>
                    control.trackingEyes != VRC_AnimatorTrackingControl.TrackingType.NoChange
                    || control.trackingMouth != VRC_AnimatorTrackingControl.TrackingType.NoChange);
        }
    }
}
