using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// コントローラのWrite Defaultsの設定を読む
    ///
    /// Modular Avatarがマージ時にWrite Defaultsを書き換える規則
    /// (MergeAnimatorProcessor) に合わせてある。規則がずれると、
    /// マージ後のFaceEmoがどちらで動くかを読み違える。
    /// </summary>
    internal static class WriteDefaultsReader
    {
        /// <summary>
        /// 全ステートのWrite Defaultsが揃っていれば、その値を返す
        /// 混ざっているか、ステートが無ければ null
        /// </summary>
        /// <param name="ignoredLayerNames">
        /// 数えないレイヤーの名前。ビルド時に取り除くレイヤーは、マージの時点ではFXに居ない
        /// </param>
        public static bool? ReadUniform(AnimatorController controller, IEnumerable<string> ignoredLayerNames)
        {
            if (controller == null)
            {
                return null;
            }

            var ignored = new HashSet<string>(
                (ignoredLayerNames ?? Enumerable.Empty<string>()).Select(FxLayerRemovalPlan.NormalizeName));
            ignored.Remove(string.Empty);

            var values = new HashSet<bool>();
            foreach (var layer in controller.layers)
            {
                if (ignored.Contains(FxLayerRemovalPlan.NormalizeName(layer.name)))
                {
                    continue;
                }

                foreach (var state in JudgedStates(layer))
                {
                    values.Add(state.writeDefaultValues);
                }
            }

            return values.Count == 1 ? values.First() : (bool?)null;
        }

        /// <summary>
        /// Write Defaultsが無効なステートを持つか
        /// </summary>
        public static bool HasWriteDefaultsOffState(AnimatorController controller)
        {
            if (controller == null)
            {
                return false;
            }

            return controller.layers.Any(layer => JudgedStates(layer).Any(state => !state.writeDefaultValues));
        }

        /// <summary>
        /// Write Defaultsの判定に数えるステート
        /// Write Defaultsを有効にするしかないレイヤーは、Modular Avatarも数えず、書き換えもしない
        /// </summary>
        private static IEnumerable<AnimatorState> JudgedStates(AnimatorControllerLayer layer)
        {
            return RequiresWriteDefaults(layer)
                ? Enumerable.Empty<AnimatorState>()
                : AnimatorGraphWalker.States(layer.stateMachine);
        }

        /// <summary>
        /// Write Defaultsを有効にするしかないレイヤーか
        /// 加算レイヤーと、Directブレンドツリーを1ステートだけで再生するレイヤーが当たる
        /// </summary>
        internal static bool RequiresWriteDefaults(AnimatorControllerLayer layer)
        {
            if (layer.blendingMode == AnimatorLayerBlendingMode.Additive)
            {
                return true;
            }

            var stateMachine = layer.stateMachine;
            if (stateMachine == null
                || stateMachine.stateMachines.Length != 0
                || stateMachine.states.Length != 1
                || stateMachine.anyStateTransitions.Length != 0)
            {
                return false;
            }

            var state = stateMachine.defaultState;
            if (state == null || state.transitions.Length != 0 || !(state.motion is BlendTree))
            {
                return false;
            }

            return ContainsDirectBlendTree(state.motion, new HashSet<Motion>());
        }

        private static bool ContainsDirectBlendTree(Motion motion, HashSet<Motion> visited)
        {
            if (!(motion is BlendTree blendTree) || !visited.Add(motion))
            {
                return false;
            }

            return blendTree.blendType == BlendTreeType.Direct
                || blendTree.children.Any(child => ContainsDirectBlendTree(child.motion, visited));
        }
    }
}
