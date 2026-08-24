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
    /// 選定ポリシーが選ぶ1つを残して自動削除するガード
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

            var components = CollectComponents(avatarRoot);
            if (components.Length <= 1)
            {
                PendingRemoval.Remove(component.GetInstanceID());
                return;
            }

            // OnValidateは編集されたコンポーネントにしか届かないため、
            // 触られた側だけを見て判断すると、もう一方が残ったままになる。
            // 触られた側が残る場合でも、漏れた側をここで削除する
            foreach (var duplicate in GenerateBridgeUseCase.SelectDuplicateComponents(avatarRoot, components))
            {
                ScheduleRemoval(avatarRoot, duplicate);
            }
        }

        private static void ScheduleRemoval(Transform avatarRoot, FEJsTBridgeComponent duplicate)
        {
            var instanceId = duplicate.GetInstanceID();
            if (!PendingRemoval.Add(instanceId))
            {
                return;
            }

            Debug.LogWarning("[FEJsTBridge] " + S("guard.log.duplicate"), duplicate);

            // OnValidateの最中はオブジェクトを破棄できないため、次のエディタ更新へ回す
            EditorApplication.delayCall += () =>
            {
                PendingRemoval.Remove(instanceId);

                if (duplicate == null || avatarRoot == null)
                {
                    return;
                }

                // 待っている間に構成が変わっていることがあるので、削除の直前に選び直す
                var refreshed = CollectComponents(avatarRoot);
                if (!GenerateBridgeUseCase.SelectDuplicateComponents(avatarRoot, refreshed).Contains(duplicate))
                {
                    return;
                }

                Undo.DestroyObjectImmediate(duplicate);

                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        S("dialog.title"),
                        S("guard.dialog.duplicate_removed"),
                        S("common.ok"));
                }
            };
        }

        private static FEJsTBridgeComponent[] CollectComponents(Transform avatarRoot)
        {
            return avatarRoot.GetComponentsInChildren<FEJsTBridgeComponent>(true)
                .Where(c => c != null)
                .ToArray();
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
