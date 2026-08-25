using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDKBase;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// AnimatorControllerから、解析にかけるレイヤーの要約を作る
    /// </summary>
    internal static class FxLayerSnapshotReader
    {
        private const string BlendShapePrefix = "blendShape.";

        public static IReadOnlyList<FxLayerSnapshot> Read(AnimatorController controller)
        {
            if (controller == null)
            {
                return new FxLayerSnapshot[0];
            }

            var layers = controller.layers;
            var snapshots = new List<FxLayerSnapshot>(layers.Length);

            for (var i = 0; i < layers.Length; i++)
            {
                snapshots.Add(new FxLayerSnapshot(
                    layers[i].name,
                    i,
                    CollectBlendShapeBindings(layers[i].stateMachine),
                    ChangesTrackingControl(layers[i].stateMachine)));
            }

            return snapshots;
        }

        /// <summary>
        /// コントローラ全体が書くブレンドシェイプ
        /// FaceEmoとJerryのコントローラから、比較対象を作るために使う
        /// </summary>
        public static IReadOnlyCollection<string> CollectBlendShapeBindings(AnimatorController controller)
        {
            if (controller == null)
            {
                return new string[0];
            }

            var bindings = new HashSet<string>();
            foreach (var layer in controller.layers)
            {
                foreach (var binding in CollectBlendShapeBindings(layer.stateMachine))
                {
                    bindings.Add(binding);
                }
            }

            return bindings;
        }

        private static IReadOnlyCollection<string> CollectBlendShapeBindings(AnimatorStateMachine stateMachine)
        {
            var bindings = new HashSet<string>();

            foreach (var state in AnimatorGraphWalker.States(stateMachine))
            {
                foreach (var clip in AnimatorGraphWalker.Clips(state.motion))
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (binding.propertyName.StartsWith(BlendShapePrefix))
                        {
                            bindings.Add(binding.path + "/" + binding.propertyName);
                        }
                    }
                }
            }

            return bindings;
        }

        /// <summary>
        /// EyesかMouthのTracking Controlを切り替えるか
        /// 切り替えるレイヤーは、ブリッジの再適用と競合する
        /// </summary>
        private static bool ChangesTrackingControl(AnimatorStateMachine stateMachine)
        {
            return AnimatorGraphWalker.Behaviours(stateMachine)
                .OfType<VRC_AnimatorTrackingControl>()
                .Any(control =>
                    control.trackingEyes != VRC_AnimatorTrackingControl.TrackingType.NoChange
                    || control.trackingMouth != VRC_AnimatorTrackingControl.TrackingType.NoChange);
        }
    }
}
