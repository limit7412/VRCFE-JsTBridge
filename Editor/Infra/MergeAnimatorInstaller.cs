using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// 生成したコントローラを専用の子GameObjectへMerge AnimatorとしてFXに登録する
    /// </summary>
    internal static class MergeAnimatorInstaller
    {
        public const string HolderObjectName = "FEJsTBridge";

        /// <summary>
        /// 生成物を載せる子オブジェクトを作り、Merge Animatorを付ける
        ///
        /// MA Parametersは付けない。Merge Animatorはコントローラのパラメータリストごと
        /// FXへマージし、MAのリネームはMA Parametersに登録された名前にしか働かないため、
        /// 生の名前で宣言しておけばJerryとFaceEmoのパラメータへそのまま接続される。
        /// </summary>
        public static GameObject Install(GameObject avatarRoot, AnimatorController controller)
        {
            if (avatarRoot == null || controller == null)
            {
                return null;
            }

            var holder = new GameObject(HolderObjectName);
            holder.transform.SetParent(avatarRoot.transform, false);

            var mergeAnimator = holder.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.deleteAttachedAnimator = true;

            // クリップが持つのは束縛先のないダミーカーブだけなので、
            // 配置場所によってパスが変わらないAbsoluteで固定する
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;

            return holder;
        }
    }
}
