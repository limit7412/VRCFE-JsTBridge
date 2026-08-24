using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
        ///
        /// NDMF 1.6以降のIAssetSaverではなくAssetContainerを使うのは、依存の下限を
        /// 1.5.0に保つため。生成するアセットは数個なので、コンテナ直付けでも支障はない。
        /// </summary>
        private static void SaveAsset(BuildContext context, Object asset)
        {
            if (asset == null)
            {
                return;
            }

            var container = context.AssetContainer;
            if (container == null || !EditorUtility.IsPersistent(container))
            {
                // Play modeのビルドなどアセット保存が無効な場合は、メモリ上のまま扱う
                return;
            }

            AssetDatabase.AddObjectToAsset(asset, container);
        }
    }
}
