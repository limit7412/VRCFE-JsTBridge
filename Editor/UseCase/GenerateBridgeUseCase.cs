using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf;
using FEJsTBridge.Domain;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.UseCase
{
    /// <summary>
    /// ブリッジ生成のユースケース
    /// コンポーネントの選定、環境検出、生成の組み立て、警告の発行を受け持つ
    /// </summary>
    internal static class GenerateBridgeUseCase
    {
        /// <summary>
        /// アバター内の処理対象コンポーネントを選定する（アバタールート直付けを優先）
        /// </summary>
        public static FEJsTBridgeComponent SelectPrimaryComponent(
            Transform avatarRoot,
            IReadOnlyList<FEJsTBridgeComponent> components)
        {
            if (components == null || components.Count == 0)
            {
                return null;
            }

            if (avatarRoot != null)
            {
                var onRoot = components.FirstOrDefault(c => c != null && c.transform == avatarRoot);
                if (onRoot != null)
                {
                    return onRoot;
                }
            }

            return components.FirstOrDefault(c => c != null);
        }

        public static FEJsTBridgeComponent SelectPrimaryComponent(
            GameObject avatarRoot,
            IReadOnlyList<FEJsTBridgeComponent> components)
        {
            return SelectPrimaryComponent(avatarRoot != null ? avatarRoot.transform : null, components);
        }

        /// <summary>
        /// 選定から漏れたコンポーネントを返す（重複の自動排除で削除する対象）
        /// ビルドが採用するものと、エディタ上に残すものを一致させるためにここへ置く
        /// </summary>
        public static FEJsTBridgeComponent[] SelectDuplicateComponents(
            Transform avatarRoot,
            IReadOnlyList<FEJsTBridgeComponent> components)
        {
            var primary = SelectPrimaryComponent(avatarRoot, components);
            if (primary == null)
            {
                return new FEJsTBridgeComponent[0];
            }

            return components.Where(c => c != null && c != primary).ToArray();
        }

        public static FEJsTBridgeComponent[] CollectComponents(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return new FEJsTBridgeComponent[0];
            }

            return avatarRoot.GetComponentsInChildren<FEJsTBridgeComponent>(true)
                .Where(c => c != null)
                .ToArray();
        }

        /// <summary>
        /// NDMFビルド時の生成処理
        ///
        /// 環境が揃っていなくても生成は続行する。供給元のないパラメータを条件に持つレイヤーと、
        /// 聞き手のいないパラメータを駆動するDriverはどちらも実行時に無害であり、
        /// 中断するとJerryやFaceEmoを後から追加する導入順に対して不必要に厳しくなる。
        /// </summary>
        public static void ExecuteForBuild(BuildContext context)
        {
            var avatarRoot = context.AvatarRootObject;
            var components = CollectComponents(avatarRoot);

            try
            {
                var primary = SelectPrimaryComponent(avatarRoot, components);
                if (primary == null)
                {
                    return;
                }

                if (components.Length > 1)
                {
                    ErrorReport.ReportError(
                        Localization.Localizer,
                        ErrorSeverity.NonFatal,
                        "warning.duplicate_component",
                        primary);
                }

                ReportEnvironment(AvatarEnvironmentScanner.Scan(avatarRoot));

                var plan = BridgePlanBuilder.Build(BridgeSettings.FromComponent(primary));
                var controller = AnimatorControllerWriter.Write(plan, asset => SaveAsset(context, asset));
                MergeAnimatorInstaller.Install(avatarRoot, controller);

                RemoveConflictingFxLayers(context, primary.removeFxLayers);
            }
            finally
            {
                // ビルド時にのみ意味を持つコンポーネントなので、生成の成否にかかわらず取り除く。
                // ビルド終盤で動作するAvatar Optimizer等から「未知のコンポーネント」として
                // 検出されないようにするため、Optimizing Phaseより前のここで削除する。
                RemoveComponents(components);
            }
        }

        public static void RemoveComponents(IReadOnlyList<FEJsTBridgeComponent> components)
        {
            if (components == null)
            {
                return;
            }

            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                Object.DestroyImmediate(component);
            }
        }

        /// <summary>
        /// 指定されたFXレイヤーを取り除く
        ///
        /// FaceEmoが書き込みを止めている間だけ表に出てくる素体の表情レイヤーが対象になる。
        /// 取り除く相手はビルド中のFXであり、素体のアセットには触れない。
        /// </summary>
        private static void RemoveConflictingFxLayers(BuildContext context, IReadOnlyList<string> requestedNames)
        {
            var avatarRoot = context.AvatarRootObject;

            // 空行だけの一覧でFXを探すと、FXの無いアバターで意味のない警告が出る
            if (!FxLayerRemovalPlan.HasRequestedName(requestedNames))
            {
                return;
            }

            var controller = FxLayerRemover.FindFxController(avatarRoot);
            if (controller == null)
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.fx_not_found");
                return;
            }

            var plan = FxLayerRemovalPlan.Resolve(FxLayerRemover.GetLayerNames(controller), requestedNames);

            foreach (var missing in plan.MissingNames)
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.layer_not_found", missing);
            }

            if (plan.IsEmpty)
            {
                return;
            }

            // ビルド中のFXはModular Avatarが複製したものである。
            // 複製はビルド用のコンテナへ保存されるため永続アセットになる。
            // プロジェクトの元アセットと区別できるのはNDMFだけなので、その判定を使う
            if (!context.IsTemporaryAsset(controller))
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.fx_not_editable");
                return;
            }

            var result = FxLayerRemover.Remove(controller, plan.LayerIndices);

            ErrorReport.ReportError(
                Localization.Localizer,
                ErrorSeverity.Information,
                "info.layers_removed",
                string.Join(", ", result.RemovedLayerNames));

            foreach (var detached in result.DetachedSyncedLayerNames)
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.synced_layer_detached", detached);
            }

            RemapFxLayerControls(context, result.NewLayerIndices);
        }

        /// <summary>
        /// FXのレイヤーを索引で指すVRCAnimatorLayerControlを、除去後の索引へ付け替える
        /// 付け替えないと、除去でずれた分だけ別のレイヤーを操作してしまう
        /// </summary>
        private static void RemapFxLayerControls(BuildContext context, IReadOnlyList<int> newLayerIndices)
        {
            var remap = FxLayerRemover.RemapFxLayerControls(
                FxLayerRemover.CollectAvatarControllers(context.AvatarRootObject),
                newLayerIndices,
                context.IsTemporaryAsset);

            foreach (var owner in remap.DetachedOwners.Distinct())
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.layer_control_detached", owner);
            }

            foreach (var skipped in remap.SkippedControllers.Distinct())
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.layer_control_not_editable", skipped);
            }
        }

        private static void ReportEnvironment(EnvironmentReport report)
        {
            if (!report.JerryDetected)
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.jerry_not_found");
            }

            if (!report.FaceEmoDetected)
            {
                ErrorReport.ReportError(
                    Localization.Localizer, ErrorSeverity.NonFatal, "warning.face_emo_not_found");
            }
        }

        /// <summary>
        /// 生成物をビルドの一時アセットへ登録する
        /// 保存済みのアセットとnullはNDMF側で無視される
        /// </summary>
        private static void SaveAsset(BuildContext context, Object asset)
        {
            context.AssetSaver.SaveAsset(asset);
        }
    }
}
