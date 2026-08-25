using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// アバターに載っているMerge Animatorを集める
        /// FaceEmoやJerryのコントローラを、パラメータ名で見分けるために使う
        /// </summary>
        public static IReadOnlyList<MergeAnimatorEntry> CollectMergeAnimatorEntries(GameObject avatarRoot)
        {
            var entries = new List<MergeAnimatorEntry>();
            if (avatarRoot == null)
            {
                return entries;
            }

            foreach (var mergeAnimator in avatarRoot.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (mergeAnimator == null || mergeAnimator.animator == null)
                {
                    continue;
                }

                var basePath = MergeAnimatorEntry.GetBasePath(mergeAnimator, avatarRoot);
                if (entries.Any(entry =>
                        entry.RuntimeController == mergeAnimator.animator && entry.BasePath == basePath))
                {
                    continue;
                }

                entries.Add(new MergeAnimatorEntry(
                    mergeAnimator.animator,
                    AnimatorControllerResolver.Resolve(mergeAnimator.animator),
                    basePath));
            }

            return entries;
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
            var controller = AnimatorControllerResolver.Resolve(runtimeController);
            if (controller == null)
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
