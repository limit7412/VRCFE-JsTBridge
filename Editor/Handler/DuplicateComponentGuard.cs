using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FEJsTBridge.UseCase;
using static FEJsTBridge.Localization;

namespace FEJsTBridge.Handler
{
    /// <summary>
    /// 同一アバター内にFEJsTBridgeComponentが複数配置された場合、
    /// 後から追加されたコンポーネントを自動削除するガード
    /// RuntimeコンポーネントのOnValidateフック経由で呼び出される
    /// </summary>
    [InitializeOnLoad]
    internal static class DuplicateComponentGuard
    {
        private static readonly HashSet<int> PendingRemoval = new HashSet<int>();

        static DuplicateComponentGuard()
        {
            FEJsTBridgeComponent.EditorOnValidateHook = EnforceSingleComponentPerAvatar;
        }

        private static void EnforceSingleComponentPerAvatar(FEJsTBridgeComponent component)
        {
            if (component == null)
            {
                return;
            }

            var avatarRoot = FindAvatarRootForUniqueness(component);
            if (avatarRoot == null)
            {
                return;
            }

            var components = avatarRoot.GetComponentsInChildren<FEJsTBridgeComponent>(true)
                .Where(c => c != null)
                .ToArray();

            var instanceId = component.GetInstanceID();
            if (components.Length <= 1)
            {
                PendingRemoval.Remove(instanceId);
                return;
            }

            var primary = GenerateBridgeUseCase.SelectPrimaryComponent(avatarRoot, components);
            if (primary == component)
            {
                PendingRemoval.Remove(instanceId);
                return;
            }

            if (!PendingRemoval.Add(instanceId))
            {
                return;
            }

            Debug.LogWarning("[FEJsTBridge] " + S("guard.log.duplicate"), component);

            // OnValidateの最中はオブジェクトを破棄できないため、次のエディタ更新へ回す
            EditorApplication.delayCall += () =>
            {
                PendingRemoval.Remove(instanceId);

                if (component == null || avatarRoot == null)
                {
                    return;
                }

                var refreshed = avatarRoot.GetComponentsInChildren<FEJsTBridgeComponent>(true)
                    .Where(c => c != null)
                    .ToArray();
                var refreshedPrimary = GenerateBridgeUseCase.SelectPrimaryComponent(avatarRoot, refreshed);

                if (refreshedPrimary != component)
                {
                    Undo.DestroyObjectImmediate(component);

                    if (!Application.isBatchMode)
                    {
                        EditorUtility.DisplayDialog(
                            S("dialog.title"),
                            S("guard.dialog.duplicate_removed"),
                            S("common.ok"));
                    }
                }
            };
        }

        private static Transform FindAvatarRootForUniqueness(FEJsTBridgeComponent component)
        {
            Transform lastDescriptorRoot = null;
            var cursor = component.transform;

            while (cursor != null)
            {
                if (HasAvatarDescriptor(cursor.gameObject))
                {
                    lastDescriptorRoot = cursor;
                }

                cursor = cursor.parent;
            }

            if (lastDescriptorRoot != null)
            {
                return lastDescriptorRoot;
            }

            return component.transform.root;
        }

        private static bool HasAvatarDescriptor(GameObject go)
        {
            return go != null && go.GetComponent("VRCAvatarDescriptor") != null;
        }
    }
}
