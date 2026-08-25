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

            // 索引のずれを避けるため後ろから取り除く
            foreach (var index in removing.OrderByDescending(index => index))
            {
                controller.RemoveLayer(index);
            }

            var detached = FixSyncedLayerIndices(controller, newIndices);

            return new FxLayerRemovalResult(removedNames, detached, newIndices);
        }

        /// <summary>
        /// FXのレイヤーを索引で指すVRCAnimatorLayerControlを、除去後の索引へ付け替える
        ///
        /// 索引は数値でしか持てないため、付け替えないと別のレイヤーを操作してしまう。
        /// 指していたレイヤーごと除去された場合は、範囲外の索引にして無効化する
        /// (VRChatは範囲外の索引を無視する)。
        /// </summary>
        /// <param name="isEditable">
        /// 書き換えてよいコントローラかの判定。ビルド中の複製かどうかはNDMFにしか分からないため、
        /// 呼び出し側から渡す
        /// </param>
        public static LayerControlRemapResult RemapFxLayerControls(
            IEnumerable<AnimatorController> controllers,
            IReadOnlyList<int> newIndices,
            Func<AnimatorController, bool> isEditable)
        {
            var detachedOwners = new List<string>();
            var skippedControllers = new List<string>();
            var remappedCount = 0;

            if (controllers == null || newIndices == null)
            {
                return LayerControlRemapResult.Empty;
            }

            foreach (var controller in controllers)
            {
                if (controller == null)
                {
                    continue;
                }

                var targets = CollectFxLayerControls(controller)
                    .Where(control => NeedsRemap(control, newIndices))
                    .ToArray();
                if (targets.Length == 0)
                {
                    continue;
                }

                // 複製されていないアセットは書き換えない
                if (isEditable != null && !isEditable(controller))
                {
                    skippedControllers.Add(controller.name);
                    continue;
                }

                foreach (var control in targets)
                {
                    var resolved = newIndices[control.layer];
                    if (resolved < 0)
                    {
                        detachedOwners.Add(controller.name);
                        control.layer = -1;
                    }
                    else
                    {
                        control.layer = resolved;
                    }

                    remappedCount++;
                }
            }

            return new LayerControlRemapResult(remappedCount, detachedOwners, skippedControllers);
        }

        private static bool NeedsRemap(VRCAnimatorLayerControl control, IReadOnlyList<int> newIndices)
        {
            if (control.playable != VRCAnimatorLayerControl.BlendableLayer.FX)
            {
                return false;
            }

            if (control.layer < 0 || control.layer >= newIndices.Count)
            {
                return false;
            }

            return newIndices[control.layer] != control.layer;
        }

        private static IEnumerable<VRCAnimatorLayerControl> CollectFxLayerControls(AnimatorController controller)
        {
            foreach (var layer in controller.layers)
            {
                foreach (var behaviour in AnimatorGraphWalker.Behaviours(layer.stateMachine))
                {
                    if (behaviour is VRCAnimatorLayerControl control && control != null)
                    {
                        yield return control;
                    }
                }
            }
        }

        /// <summary>
        /// 残ったSyncedレイヤーの参照先を、除去後の索引へ付け替える
        /// 参照先ごと取り除かれていた場合は参照を外し、その名前を返す
        /// </summary>
        private static IReadOnlyList<string> FixSyncedLayerIndices(AnimatorController controller, int[] newIndices)
        {
            var layers = controller.layers;
            var detached = new List<string>();
            var changed = false;

            for (var i = 0; i < layers.Length; i++)
            {
                var synced = layers[i].syncedLayerIndex;
                if (synced < 0 || synced >= newIndices.Length)
                {
                    continue;
                }

                var resolved = newIndices[synced];
                if (resolved == synced)
                {
                    continue;
                }

                if (resolved < 0)
                {
                    detached.Add(layers[i].name);
                }

                layers[i].syncedLayerIndex = resolved;
                changed = true;
            }

            if (changed)
            {
                // layersはコピーを返すプロパティなので、書き戻すまで反映されない
                controller.layers = layers;
            }

            return detached;
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
