using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// RuntimeAnimatorControllerから実体のAnimatorControllerを取り出す
    /// </summary>
    internal static class AnimatorControllerResolver
    {
        /// <summary>
        /// Override Controllerは元のコントローラまで辿る
        /// 辿れなければ null を返す
        /// </summary>
        public static AnimatorController Resolve(RuntimeAnimatorController runtimeController)
        {
            var resolved = runtimeController;
            while (resolved is AnimatorOverrideController overrideController)
            {
                resolved = overrideController.runtimeAnimatorController;
            }

            return resolved as AnimatorController;
        }

        /// <summary>
        /// Override Controllerによるクリップの差し替えを、元のクリップからの対応表にする
        /// </summary>
        /// <remarks>
        /// 鍵はどの階層でも元のコントローラのクリップである。
        /// 内側から順に重ね、外側で指定があればそちらで上書きする。
        /// 外側が未指定のままなら、内側の差し替えがそのまま残る。
        /// </remarks>
        public static IReadOnlyDictionary<AnimationClip, AnimationClip> CollectOverrides(
            RuntimeAnimatorController runtimeController)
        {
            var chain = new List<AnimatorOverrideController>();
            var current = runtimeController;
            while (current is AnimatorOverrideController overrideController)
            {
                chain.Add(overrideController);
                current = overrideController.runtimeAnimatorController;
            }

            var map = new Dictionary<AnimationClip, AnimationClip>();
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                foreach (var pair in GetOverrides(chain[i]))
                {
                    map[pair.Key] = pair.Value;
                }
            }

            return map;
        }

        /// <summary>
        /// 対応表に従ってクリップを差し替える
        /// 対応がなければ元のまま
        /// </summary>
        public static AnimationClip Apply(
            IReadOnlyDictionary<AnimationClip, AnimationClip> overrides, AnimationClip clip)
        {
            if (clip == null || overrides == null)
            {
                return clip;
            }

            return overrides.TryGetValue(clip, out var replaced) ? replaced : clip;
        }

        private static Dictionary<AnimationClip, AnimationClip> GetOverrides(
            AnimatorOverrideController overrideController)
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(pairs);

            var level = new Dictionary<AnimationClip, AnimationClip>();
            foreach (var pair in pairs)
            {
                // 差し替えが設定されていないスロットはValueがnullになる
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                level[pair.Key] = pair.Value;
            }

            return level;
        }
    }
}
