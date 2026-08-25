using System.Collections.Generic;
using System.Linq;
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

            var entries = AvatarEnvironmentScanner.CollectMergeAnimatorEntries(avatarRoot);
            var faceEmo = FindByParameter(entries, BridgeParameterNames.ForceBypassEnable);
            var jerry = FindByParameter(
                entries,
                BridgeParameterNames.FacialExpressionsDisabled,
                BridgeParameterNames.EyeTrackingActive);

            // 表情とトラッキングが書くブレンドシェイプを、競合の判定基準にする
            var reference = new HashSet<string>();
            foreach (var entry in new[] { faceEmo, jerry }.Where(e => e != null))
            {
                foreach (var binding in
                    FxLayerSnapshotReader.CollectBlendShapeBindings(entry.RuntimeController, entry.BasePath))
                {
                    reference.Add(binding);
                }
            }

            var report = FxLayerConflictAnalyzer.Analyze(FxLayerSnapshotReader.Read(fx), reference);

            return new FxLayerInspection(report, faceEmo != null, jerry != null);
        }

        private static MergeAnimatorEntry FindByParameter(
            IEnumerable<MergeAnimatorEntry> entries,
            params string[] requiredParameters)
        {
            return entries.FirstOrDefault(entry =>
            {
                if (entry.Controller == null)
                {
                    return false;
                }

                var names = entry.Controller.parameters.Select(parameter => parameter.name).ToArray();
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
