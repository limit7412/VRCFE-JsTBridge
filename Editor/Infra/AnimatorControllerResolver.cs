using System.Collections.Generic;
using System.Linq;
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
        /// Override Controllerが入れ子になっていれば、内側から順に重ねる。
        /// 内側でA→B、外側でB→Cなら、最終的にA→Cとして扱う。
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
                var level = GetOverrides(chain[i]);

                // すでに積んである差し替え先が、さらに外側で差し替えられていれば辿る
                foreach (var key in map.Keys.ToArray())
                {
                    if (level.TryGetValue(map[key], out var replaced))
                    {
                        map[key] = replaced;
                    }
                }

                foreach (var pair in level)
                {
                    if (!map.ContainsKey(pair.Key))
                    {
                        map[pair.Key] = pair.Value;
                    }
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
