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
    }
}
