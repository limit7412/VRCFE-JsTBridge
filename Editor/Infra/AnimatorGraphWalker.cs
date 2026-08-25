using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// ステートマシンとモーションを辿るための共通処理
    /// </summary>
    internal static class AnimatorGraphWalker
    {
        /// <summary>サブステートマシンを含む全ステート</summary>
        public static IEnumerable<AnimatorState> States(AnimatorStateMachine stateMachine)
        {
            if (stateMachine == null)
            {
                yield break;
            }

            foreach (var state in stateMachine.states)
            {
                if (state.state != null)
                {
                    yield return state.state;
                }
            }

            foreach (var child in stateMachine.stateMachines)
            {
                foreach (var state in States(child.stateMachine))
                {
                    yield return state;
                }
            }
        }

        /// <summary>ステートマシン自身とステートが持つ全behaviour</summary>
        public static IEnumerable<StateMachineBehaviour> Behaviours(AnimatorStateMachine stateMachine)
        {
            if (stateMachine == null)
            {
                yield break;
            }

            foreach (var behaviour in stateMachine.behaviours)
            {
                yield return behaviour;
            }

            foreach (var state in stateMachine.states)
            {
                if (state.state == null)
                {
                    continue;
                }

                foreach (var behaviour in state.state.behaviours)
                {
                    yield return behaviour;
                }
            }

            foreach (var child in stateMachine.stateMachines)
            {
                foreach (var behaviour in Behaviours(child.stateMachine))
                {
                    yield return behaviour;
                }
            }
        }

        /// <summary>
        /// モーションに含まれるクリップ
        /// ブレンドツリーは入れ子をたどる
        /// </summary>
        public static IEnumerable<AnimationClip> Clips(Motion motion)
        {
            return Clips(motion, new HashSet<Motion>());
        }

        private static IEnumerable<AnimationClip> Clips(Motion motion, HashSet<Motion> visited)
        {
            if (motion == null || !visited.Add(motion))
            {
                yield break;
            }

            if (motion is AnimationClip clip)
            {
                yield return clip;
                yield break;
            }

            if (motion is BlendTree blendTree)
            {
                foreach (var child in blendTree.children)
                {
                    foreach (var childClip in Clips(child.motion, visited))
                    {
                        yield return childClip;
                    }
                }
            }
        }
    }
}
