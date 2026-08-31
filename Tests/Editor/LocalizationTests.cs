using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using FEJsTBridge.Presentation;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// 翻訳ファイルの検証
    ///
    /// NDMFのLocalizerは、引けなかったキーを既定言語 (ja-jp) から補う。
    /// 実行時の引き当てを見るだけでは英語の訳抜けが表に出ないため、poファイルを直接読む。
    /// </summary>
    public class LocalizationTests
    {
        private static readonly string[] Languages = { "ja-jp", "en-us" };

        /// <summary>
        /// インスペクタとビルド時の警告が使う文言のキー
        /// ツールチップ (.tooltip) は任意のため、ここには含めない
        /// </summary>
        private static readonly string[] RequiredKeys =
        {
            "common.ok",
            "common.cancel",
            "dialog.title",
            "inspector.description",
            "inspector.bypass_trigger.lip_tracking_only",
            "inspector.tracking_reapply.disabled",
            "prop.bypass_trigger",
            "prop.enable_tracking_reapply",
            "prop.reapply_delay_seconds",
            "prop.remove_fx_layers",
            "inspector.remove_fx_layers",
            "warning.fx_not_found",
            "warning.fx_not_found:description",
            "warning.layer_not_found",
            "warning.layer_not_found:description",
            "warning.synced_layer_detached",
            "warning.synced_layer_detached:description",
            "info.layers_removed",
            "info.layers_removed:description",
            "warning.fx_not_editable",
            "warning.fx_not_editable:description",
            "warning.layer_control_detached",
            "warning.layer_control_detached:description",
            "warning.layer_control_not_editable",
            "warning.layer_control_not_editable:description",
            "inspector.inspect.button",
            "inspector.inspect.multi_edit",
            "inspector.inspect.no_fx",
            "inspector.inspect.no_reference",
            "inspector.inspect.empty_reference",
            "inspector.inspect.write_defaults",
            "inspector.inspect.no_face_emo",
            "inspector.inspect.no_jerry",
            "inspector.inspect.no_candidate",
            "inspector.inspect.candidates",
            "inspector.inspect.others",
            "inspector.inspect.layer",
            "inspector.inspect.layer_index",
            "inspector.inspect.same_name",
            "inspector.inspect.empty_name",
            "inspector.inspect.add",
            "inspector.inspect.add_all",
            "inspector.inspect.reason.shared",
            "inspector.inspect.reason.blend_shapes",
            "inspector.inspect.reason.tracking_control",
            "inspector.inspect.verdict.no_conflict",
            "inspector.inspect.verdict.managed",
            "warning.jerry_not_found",
            "warning.jerry_not_found:description",
            "warning.face_emo_not_found",
            "warning.face_emo_not_found:description",
            "warning.duplicate_component",
            "warning.duplicate_component:description",
            "guard.log.duplicate",
            "guard.dialog.duplicate_removed",
            "update.available.vpm",
            "update.available.booth",
            "update.available.unknown",
            "update.open_releases",
            "update.dismiss",
            "update.settings.enabled",
            "update.settings.description",
            "update.available.booth_updatable",
            "update.run",
            "update.confirm",
            "update.announce.message",
            "update.announce.update",
            "update.announce.later",
            "update.completed",
            "update.incomplete",
            "update.error.unsupported",
            "update.error.release_missing",
            "update.error.asset_missing",
            "update.error.download",
            "update.error.digest",
            "update.error.archive",
            "update.error.contents",
            "update.error.version",
            "update.error.scan",
            "update.error.import",
            "update.error.import_cancelled",
            "update.error.backup",
            "update.error.delete",
            "update.error.playing",
        };

        /// <summary>
        /// poが取り込まれた結果の型を、名前で確かめる
        /// </summary>
        /// <remarks>
        /// LocalizationAssetを型として書かないのは、このアセンブリから解決できないためである。
        /// FEJsTBridge.Editorでは書けるので、テスト側の参照の問題だと分かるが、
        /// 確かめたいのは「poがLocalizationAssetとして取り込まれること」であり、
        /// 型名の一致で足りる。
        /// </remarks>
        [Test]
        public void PoFiles_AreImportedAsLocalizationAssets()
        {
            foreach (var language in Languages)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PathFor(language));

                Assert.That(asset, Is.Not.Null, $"{language}.poが読み込めない");
                Assert.That(
                    asset.GetType().Name,
                    Is.EqualTo("LocalizationAsset"),
                    $"{language}.poがLocalizationAssetとして取り込まれていない");
            }
        }

        [Test]
        public void PoFiles_HaveIdenticalKeySets()
        {
            var japanese = ReadKeys("ja-jp");
            var english = ReadKeys("en-us");

            Assert.That(english.Except(japanese), Is.Empty, "ja-jpに無いキーがen-usにある");
            Assert.That(japanese.Except(english), Is.Empty, "en-usに無いキーがja-jpにある");
        }

        [Test]
        public void PoFiles_ContainEveryRequiredKey()
        {
            foreach (var language in Languages)
            {
                var keys = ReadKeys(language);
                foreach (var required in RequiredKeys)
                {
                    Assert.That(keys, Contains.Item(required), $"{language}.poに{required}が無い");
                }
            }
        }

        [Test]
        public void BypassTriggerLabelKeys_CoverEveryEnumValue()
        {
            var labelKeys = FEJsTBridgeComponentEditor.BypassTriggerLabelKeys;

            // 並びは宣言順に一致させる必要がある (enumValueIndexで引くため)
            Assert.That(labelKeys.Count, Is.EqualTo(Enum.GetValues(typeof(BypassTrigger)).Length));

            foreach (var language in Languages)
            {
                var keys = ReadKeys(language);
                foreach (var labelKey in labelKeys)
                {
                    Assert.That(keys, Contains.Item(labelKey), $"{language}.poに{labelKey}が無い");
                }
            }
        }

        private static string PathFor(string language)
        {
            return $"{Localization.GetLocalizationRoot()}/{language}.po";
        }

        private static IReadOnlyCollection<string> ReadKeys(string language)
        {
            var path = PathFor(language);
            Assert.That(File.Exists(path), Is.True, $"{path}が見つからない");

            var keys = new HashSet<string>();
            foreach (Match match in Regex.Matches(File.ReadAllText(path), "^msgid \"(.*)\"", RegexOptions.Multiline))
            {
                var key = match.Groups[1].Value;

                // 先頭の空msgidはpoのヘッダー
                if (key.Length > 0)
                {
                    keys.Add(key);
                }
            }

            return keys;
        }
    }
}
