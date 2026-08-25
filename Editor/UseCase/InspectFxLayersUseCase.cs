using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using FEJsTBridge.Domain;
using FEJsTBridge.Infra;

namespace FEJsTBridge.UseCase
{
    /// <summary>
    /// 除去するFXレイヤーを調べるユースケース
    ///
    /// 解析するのはアバター自身のFXである。
    /// Merge Animatorであとからマージされるレイヤーは除去できないため、対象に含めない。
    /// </summary>
    internal static class InspectFxLayersUseCase
    {
        public static FxLayerInspection Inspect(GameObject avatarRoot)
        {
            var fx = FxLayerRemover.FindFxController(avatarRoot);
            if (fx == null)
            {
                return FxLayerInspection.NoFx;
            }

            var mergeAnimatorControllers = AvatarEnvironmentScanner.CollectMergeAnimatorControllers(avatarRoot);
            var faceEmo = FindByParameter(mergeAnimatorControllers, BridgeParameterNames.ForceBypassEnable);
            var jerry = FindByParameter(
                mergeAnimatorControllers,
                BridgeParameterNames.FacialExpressionsDisabled,
                BridgeParameterNames.EyeTrackingActive);

            // 表情とトラッキングが書くブレンドシェイプを、競合の判定基準にする
            var reference = new HashSet<string>();
            foreach (var controller in new[] { faceEmo, jerry }.Where(c => c != null))
            {
                foreach (var binding in FxLayerSnapshotReader.CollectBlendShapeBindings(controller))
                {
                    reference.Add(binding);
                }
            }

            var report = FxLayerConflictAnalyzer.Analyze(FxLayerSnapshotReader.Read(fx), reference);

            return new FxLayerInspection(report, faceEmo != null, jerry != null);
        }

        private static AnimatorController FindByParameter(
            IEnumerable<AnimatorController> controllers,
            params string[] requiredParameters)
        {
            return controllers.FirstOrDefault(controller =>
            {
                var names = controller.parameters.Select(parameter => parameter.name).ToArray();
                return requiredParameters.All(names.Contains);
            });
        }
    }

    internal sealed class FxLayerInspection
    {
        public static readonly FxLayerInspection NoFx = new FxLayerInspection(null, false, false);

        public FxLayerInspection(FxLayerConflictReport report, bool faceEmoFound, bool jerryFound)
        {
            Report = report;
            FaceEmoFound = faceEmoFound;
            JerryFound = jerryFound;
        }

        /// <summary>FXが見つからなければ null</summary>
        public FxLayerConflictReport Report { get; }

        public bool FxFound => Report != null;

        public bool FaceEmoFound { get; }

        public bool JerryFound { get; }
    }
}
