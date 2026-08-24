using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// アバターに載っているMerge Animatorを走査し、Jerry's TemplatesとFaceEmoの有無を調べる
    /// </summary>
    internal static class AvatarEnvironmentScanner
    {
        /// <summary>
        /// 非アクティブなオブジェクトも走査対象に含める
        /// 導入直後にオフのまま置かれたプレハブを未導入と誤検出しないため
        /// </summary>
        public static EnvironmentReport Scan(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return new EnvironmentReport(false, false);
            }

            return EnvironmentReport.Detect(CollectParameterNames(avatarRoot));
        }

        private static IEnumerable<IReadOnlyCollection<string>> CollectParameterNames(GameObject avatarRoot)
        {
            foreach (var mergeAnimator in avatarRoot.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (mergeAnimator == null)
                {
                    continue;
                }

                var names = GetParameterNames(mergeAnimator.animator);
                if (names != null)
                {
                    yield return names;
                }
            }
        }

        /// <summary>
        /// コントローラが宣言するパラメータ名を取り出す
        /// Override Controllerは元のコントローラまで辿る
        /// </summary>
        internal static IReadOnlyCollection<string> GetParameterNames(RuntimeAnimatorController runtimeController)
        {
            var resolved = runtimeController;
            while (resolved is AnimatorOverrideController overrideController)
            {
                resolved = overrideController.runtimeAnimatorController;
            }

            if (!(resolved is AnimatorController controller))
            {
                return null;
            }

            var names = new List<string>(controller.parameters.Length);
            foreach (var parameter in controller.parameters)
            {
                names.Add(parameter.name);
            }

            return names;
        }
    }
}
