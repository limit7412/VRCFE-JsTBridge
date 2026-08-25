using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// アバターに載っているMerge Animator一件分
    ///
    /// マージ後のクリップと突き合わせるには、コントローラだけでは足りない。
    /// Override Controllerの差し替えと、Relativeモードのパス前置も一緒に持つ。
    /// </summary>
    internal sealed class MergeAnimatorEntry
    {
        public MergeAnimatorEntry(
            RuntimeAnimatorController runtimeController,
            AnimatorController controller,
            string basePath)
        {
            RuntimeController = runtimeController;
            Controller = controller;
            BasePath = basePath ?? string.Empty;
        }

        /// <summary>Merge Animatorに指定されたコントローラそのもの</summary>
        public RuntimeAnimatorController RuntimeController { get; }

        /// <summary>
        /// パラメータ定義を読むために解決した実体
        /// Override Controllerしか辿れなければ null
        /// </summary>
        public AnimatorController Controller { get; }

        /// <summary>
        /// マージ時にクリップのパスへ前置される文字列
        /// Absoluteモードなら空
        /// </summary>
        public string BasePath { get; }

        /// <summary>
        /// Modular Avatarがマージ時に使う前置パスを、同じ規則で求める
        /// </summary>
        /// <remarks>
        /// 規則はModularAvatarMergeAnimator.GetMotionBasePathCallbackに合わせている。
        /// Absoluteなら前置なし、RelativeならrelativePathRoot、
        /// 指定がなければコンポーネントの載っているオブジェクトまでの相対パスになる。
        /// </remarks>
        public static string GetBasePath(ModularAvatarMergeAnimator mergeAnimator, GameObject avatarRoot)
        {
            if (mergeAnimator == null || avatarRoot == null)
            {
                return string.Empty;
            }

            if (mergeAnimator.pathMode == MergeAnimatorPathMode.Absolute)
            {
                return string.Empty;
            }

            var target = mergeAnimator.relativePathRoot != null
                ? mergeAnimator.relativePathRoot.Get(avatarRoot.transform)
                : null;
            if (target == null)
            {
                target = mergeAnimator.gameObject;
            }

            return RuntimeUtil.RelativePath(avatarRoot, target) ?? string.Empty;
        }
    }
}
