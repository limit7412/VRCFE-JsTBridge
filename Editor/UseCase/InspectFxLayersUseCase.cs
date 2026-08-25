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
            var fx = FxLayerRemover.FindFxRuntimeController(avatarRoot);
            if (AnimatorControllerResolver.Resolve(fx) == null)
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
            foreach (var entry in faceEmo.Concat(jerry))
            {
                foreach (var binding in
                    FxLayerSnapshotReader.CollectBlendShapeBindings(entry.RuntimeController, entry.BasePath))
                {
                    reference.Add(binding);
                }
            }

            var report = FxLayerConflictAnalyzer.Analyze(FxLayerSnapshotReader.Read(fx), reference);

            return new FxLayerInspection(report, faceEmo.Count > 0, jerry.Count > 0);
        }

        /// <summary>
        /// 指定したパラメータを持つMerge Animatorを集める
        /// </summary>
        /// <remarks>
        /// 一件だけを選ばないのは、同じツールが複数のMerge Animatorに分かれていることがあるためである。
        /// マージ先が違えば束縛のパスも変わるので、取りこぼすと競合を見落とす。
        /// </remarks>
        private static IReadOnlyList<MergeAnimatorEntry> FindByParameter(
            IEnumerable<MergeAnimatorEntry> entries,
            params string[] requiredParameters)
        {
            return entries.Where(entry =>
            {
                if (entry.Controller == null)
                {
                    return false;
                }

                var names = entry.Controller.parameters.Select(parameter => parameter.name).ToArray();
                return requiredParameters.All(names.Contains);
            }).ToArray();
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
