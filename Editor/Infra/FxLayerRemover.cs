using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using UnityEngine;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// アバターのFXから指定のレイヤーを取り除く
    ///
    /// 取り除く相手はビルド中のFXであり、素体のアセットではない。
    /// Modular AvatarがResolvingフェーズで入力コントローラを複製するため、
    /// Generatingフェーズで触るコントローラはすでに複製されている。
    /// </summary>
    internal static class FxLayerRemover
    {
        /// <summary>
        /// アバターディスクリプタからFXのコントローラを取り出す
        /// 既定のコントローラのままなら null を返す
        /// </summary>
        public static AnimatorController FindFxController(GameObject avatarRoot)
        {
            var descriptor = avatarRoot != null ? avatarRoot.GetComponent<VRCAvatarDescriptor>() : null;
            if (descriptor == null)
            {
                return null;
            }

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    return layer.animatorController as AnimatorController;
                }
            }

            return null;
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
                return new FxLayerRemovalResult(new string[0], new string[0]);
            }

            var layers = controller.layers;
            var removing = new HashSet<int>(layerIndices.Where(index => index >= 0 && index < layers.Length));
            if (removing.Count == 0)
            {
                return new FxLayerRemovalResult(new string[0], new string[0]);
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

            return new FxLayerRemovalResult(removedNames, detached);
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
    }

    internal sealed class FxLayerRemovalResult
    {
        public FxLayerRemovalResult(IReadOnlyList<string> removedLayerNames, IReadOnlyList<string> detachedSyncedLayerNames)
        {
            RemovedLayerNames = removedLayerNames;
            DetachedSyncedLayerNames = detachedSyncedLayerNames;
        }

        public IReadOnlyList<string> RemovedLayerNames { get; }

        /// <summary>参照先を失ったSyncedレイヤーの名前</summary>
        public IReadOnlyList<string> DetachedSyncedLayerNames { get; }
    }
}
