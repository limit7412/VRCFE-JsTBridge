using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// アバターのFXから指定のレイヤーを取り除く
    ///
    /// 取り除く相手はビルド中のFXであり、素体のアセットではない。
    /// Modular AvatarがResolvingフェーズで入力コントローラを複製するため、
    /// Generatingフェーズで触るコントローラはすでに複製されている。
    /// 複製されていないアセットを渡された場合に備え、書き換える側で永続アセットかを確かめる。
    /// </summary>
    internal static class FxLayerRemover
    {
        /// <summary>
        /// アバターディスクリプタからFXのコントローラを取り出す
        /// 既定のコントローラのままなら null を返す
        /// </summary>
        public static AnimatorController FindFxController(GameObject avatarRoot)
        {
            return AnimatorControllerResolver.Resolve(FindFxRuntimeController(avatarRoot));
        }

        /// <summary>
        /// FXに指定されたコントローラを、Override Controllerのまま取り出す
        /// 何が再生されるかを調べるには、差し替えの情報が要る
        /// </summary>
        public static RuntimeAnimatorController FindFxRuntimeController(GameObject avatarRoot)
        {
            var descriptor = FindDescriptor(avatarRoot);
            if (descriptor == null)
            {
                return null;
            }

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type != VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    continue;
                }

                // Defaultを選んだあとも参照が残ることがある。
                // ビルドでは無視されるコントローラなので、取り除いても出力に反映されない
                return layer.isDefault ? null : layer.animatorController;
            }

            return null;
        }

        /// <summary>
        /// アバターが持つ全プレイアブルレイヤーのコントローラを集める
        /// FXのレイヤーを指すVRCAnimatorLayerControlは、FX以外のコントローラにも置かれうる
        /// </summary>
        public static IReadOnlyList<AnimatorController> CollectAvatarControllers(GameObject avatarRoot)
        {
            var descriptor = FindDescriptor(avatarRoot);
            if (descriptor == null)
            {
                return new AnimatorController[0];
            }

            var controllers = new List<AnimatorController>();

            foreach (var layer in descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers))
            {
                if (layer.isDefault)
                {
                    continue;
                }

                var controller = AnimatorControllerResolver.Resolve(layer.animatorController);
                if (controller != null && !controllers.Contains(controller))
                {
                    controllers.Add(controller);
                }
            }

            return controllers;
        }

        public static IReadOnlyList<string> GetLayerNames(AnimatorController controller)
        {
            if (controller == null)
            {
                return new string[0];
            }

            return controller.layers.Select(layer => layer.name).ToArray();
        }

        /// <summary>
        /// 指定索引のレイヤーを取り除く
        /// </summary>
        /// <summary>
        /// 指定した索引のレイヤーを取り除く
        /// </summary>
        /// <remarks>
        /// AnimatorController.RemoveLayerは使わない。
        /// 除去の直後は同期レイヤーのsyncedLayerIndexが古い索引を指しており、
        /// 自分自身や範囲外を指す状態になる。Unityはこの構成を受け付けず、
        /// 同期レイヤーごと消してしまう。
        /// 除去と索引の付け替えをまとめて一度で書き戻し、その状態を作らない。
        /// </remarks>
        public static FxLayerRemovalResult Remove(AnimatorController controller, IReadOnlyList<int> layerIndices)
        {
            if (controller == null || layerIndices == null || layerIndices.Count == 0)
            {
                return FxLayerRemovalResult.Empty;
            }

            var layers = controller.layers;
            var removing = new HashSet<int>(layerIndices.Where(index => index >= 0 && index < layers.Length));
            if (removing.Count == 0)
            {
                return FxLayerRemovalResult.Empty;
            }

            var removedNames = removing.OrderBy(index => index).Select(index => layers[index].name).ToArray();

            // 除去後の索引への対応表。除去されるレイヤーは -1
            var newIndices = new int[layers.Length];
            var next = 0;
            for (var i = 0; i < layers.Length; i++)
            {
                newIndices[i] = removing.Contains(i) ? -1 : next++;
            }

            var kept = new List<AnimatorControllerLayer>(layers.Length - removing.Count);
            var detached = new List<string>();

            for (var i = 0; i < layers.Length; i++)
            {
                if (removing.Contains(i))
                {
                    continue;
                }

                var layer = layers[i];
                var synced = layer.syncedLayerIndex;
                if (synced >= 0)
                {
                    var resolved = synced < newIndices.Length ? newIndices[synced] : -1;
                    if (resolved < 0)
                    {
                        detached.Add(layer.name);
                    }

                    layer.syncedLayerIndex = resolved;
                }

                kept.Add(layer);
            }

            controller.layers = kept.ToArray();

            return new FxLayerRemovalResult(removedNames, detached, newIndices);
        }


        private static VRCAvatarDescriptor FindDescriptor(GameObject avatarRoot)
        {
            return avatarRoot != null ? avatarRoot.GetComponent<VRCAvatarDescriptor>() : null;
        }
    }

    internal sealed class FxLayerRemovalResult
    {
        public static readonly FxLayerRemovalResult Empty =
            new FxLayerRemovalResult(new string[0], new string[0], new int[0]);

        public FxLayerRemovalResult(
            IReadOnlyList<string> removedLayerNames,
            IReadOnlyList<string> detachedSyncedLayerNames,
            IReadOnlyList<int> newLayerIndices)
        {
            RemovedLayerNames = removedLayerNames;
            DetachedSyncedLayerNames = detachedSyncedLayerNames;
            NewLayerIndices = newLayerIndices;
        }

        public IReadOnlyList<string> RemovedLayerNames { get; }

        /// <summary>参照先を失ったSyncedレイヤーの名前</summary>
        public IReadOnlyList<string> DetachedSyncedLayerNames { get; }

        /// <summary>除去前の索引から除去後の索引への対応表。除去されたレイヤーは -1</summary>
        public IReadOnlyList<int> NewLayerIndices { get; }
    }

    internal sealed class LayerControlRemapResult
    {
        public static readonly LayerControlRemapResult Empty =
            new LayerControlRemapResult(0, new string[0], new string[0]);

        public LayerControlRemapResult(
            int remappedCount,
            IReadOnlyList<string> detachedOwners,
            IReadOnlyList<string> skippedControllers)
        {
            RemappedCount = remappedCount;
            DetachedOwners = detachedOwners;
            SkippedControllers = skippedControllers;
        }

        public int RemappedCount { get; }

        /// <summary>指していたレイヤーごと除去されたVRCAnimatorLayerControlを持つコントローラ名</summary>
        public IReadOnlyList<string> DetachedOwners { get; }

        /// <summary>永続アセットのため書き換えなかったコントローラ名</summary>
        public IReadOnlyList<string> SkippedControllers { get; }
    }
}
