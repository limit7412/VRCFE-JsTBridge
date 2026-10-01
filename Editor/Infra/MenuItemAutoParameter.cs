using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using FEJsTBridge.Domain;
using RuntimeUtil = nadena.dev.ndmf.runtime.RuntimeUtil;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// パラメータ名が空のMA Menu Itemについて、MAが名前を振るかどうかと、振る名前を求める
    /// </summary>
    internal static class MenuItemAutoParameter
    {
        /// <summary>
        /// MAがビルド中にパラメータ名を振る対象かどうか
        ///
        /// MAのParameterAssignerPass.ShouldAssignParametersToMamiと同じ規則で、
        /// トグルかボタンであり、かつ自身か配下にReactive Component (Object ToggleやShape Changerなど) を
        /// 持ち、そのコンポーネントから見て最も近いメニューアイテムが自身であるものを対象とする
        /// </summary>
        public static bool IsAssigned(ModularAvatarMenuItem item)
        {
            var control = item != null ? item.Control : null;
            if (control == null
                || (control.type != VRCExpressionsMenu.Control.ControlType.Toggle
                    && control.type != VRCExpressionsMenu.Control.ControlType.Button))
            {
                return false;
            }

            foreach (var reactive in item.GetComponentsInChildren<ReactiveComponent>(true))
            {
                if (reactive.transform == item.transform)
                {
                    return true;
                }

                // MAと同じく、非アクティブな親を飛ばす既定の探索に合わせる
                if (reactive.GetComponentInParent<ModularAvatarMenuItem>() == item)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// MAが振る名前を、アバタールートからのパスから組み立てる
        /// </summary>
        public static string NameOf(ModularAvatarMenuItem item, GameObject avatarRoot)
        {
            var path = RuntimeUtil.RelativePath(avatarRoot, item.gameObject) ?? string.Empty;
            return MenuItemAutoParameterName.Build(item.gameObject.name, path);
        }
    }
}
