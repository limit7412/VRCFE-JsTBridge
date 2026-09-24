using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static FEJsTBridge.Localization;

namespace FEJsTBridge.Presentation
{
    /// <summary>
    /// 追加パラメータ1件分の描画
    ///
    /// 指定方法に応じて、使う側のフィールドだけを出す。
    /// 使わない側の値はシリアライズされたまま残るため、指定方法を戻せば元の設定に戻る。
    /// </summary>
    [CustomPropertyDrawer(typeof(ExtraParameterEntry))]
    internal class ExtraParameterEntryDrawer : PropertyDrawer
    {
        /// <summary>並びはExtraParameterSourceの宣言順に一致させる (enumValueIndexで引くため)</summary>
        internal static readonly IReadOnlyList<string> SourceLabelKeys = new[]
        {
            "prop.extra_parameter.source.menu_item",
            "prop.extra_parameter.source.parameter_name",
        };

        /// <summary>並びはExtraToggleStateの宣言順に一致させる (enumValueIndexで引くため)</summary>
        internal static readonly IReadOnlyList<string> ToggleStateLabelKeys = new[]
        {
            "prop.extra_parameter.state.on",
            "prop.extra_parameter.state.off",
        };

        /// <summary>並びはExtraReleaseModeの宣言順に一致させる (enumValueIndexで引くため)</summary>
        internal static readonly IReadOnlyList<string> ReleaseModeLabelKeys = new[]
        {
            "prop.extra_parameter.release_mode.revert",
            "prop.extra_parameter.release_mode.keep",
            "prop.extra_parameter.release_mode.restore",
        };

        /// <summary>並びはExtraSyncModeの宣言順に一致させる (enumValueIndexで引くため)</summary>
        internal static readonly IReadOnlyList<string> SyncModeLabelKeys = new[]
        {
            "prop.extra_parameter.sync_mode.auto",
            "prop.extra_parameter.sync_mode.synced",
            "prop.extra_parameter.sync_mode.unsynced",
        };

        private static float LineHeight => EditorGUIUtility.singleLineHeight;

        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lines = 1;
            if (property.isExpanded)
            {
                lines += CountFieldLines(property);
            }

            return lines * LineHeight + (lines - 1) * Spacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, LineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, DescribeEntry(property), true);

            if (property.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawFields(ref line, property);
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 折りたたんだときにも、どのパラメータを指しているかが分かる見出しにする
        /// </summary>
        private static GUIContent DescribeEntry(SerializedProperty property)
        {
            var source = property.FindPropertyRelative(nameof(ExtraParameterEntry.source));

            string target;
            if (source.enumValueIndex == (int)ExtraParameterSource.MenuItem)
            {
                var menuItem = property.FindPropertyRelative(nameof(ExtraParameterEntry.menuItem))
                    .objectReferenceValue;
                target = menuItem != null ? menuItem.name : "-";
            }
            else
            {
                var name = property.FindPropertyRelative(nameof(ExtraParameterEntry.parameterName)).stringValue;
                target = string.IsNullOrEmpty(name) ? "-" : name;
            }

            return new GUIContent(target);
        }

        private static int CountFieldLines(SerializedProperty property)
        {
            var source = property.FindPropertyRelative(nameof(ExtraParameterEntry.source));

            // 指定方法、指定の本体、トラッキング中の値 (状態)、解除時、同期
            var lines = 5;

            if (source.enumValueIndex == (int)ExtraParameterSource.ParameterName)
            {
                // 型の行が加わる
                lines++;

                if (IsRevert(property))
                {
                    // 解除時の値の行が加わる
                    lines++;
                }
            }

            return lines;
        }

        private static void DrawFields(ref Rect line, SerializedProperty property)
        {
            var source = property.FindPropertyRelative(nameof(ExtraParameterEntry.source));

            NextLine(ref line);
            DrawLocalizedEnumPopup(line, source, "prop.extra_parameter.source", SourceLabelKeys);

            if (source.enumValueIndex == (int)ExtraParameterSource.MenuItem)
            {
                NextLine(ref line);
                EditorGUI.PropertyField(
                    line,
                    property.FindPropertyRelative(nameof(ExtraParameterEntry.menuItem)),
                    G("prop.extra_parameter.menu_item"));

                NextLine(ref line);
                DrawLocalizedEnumPopup(
                    line,
                    property.FindPropertyRelative(nameof(ExtraParameterEntry.menuItemState)),
                    "prop.extra_parameter.menu_item_state",
                    ToggleStateLabelKeys);

                NextLine(ref line);
                DrawReleaseMode(line, property);

                NextLine(ref line);
                DrawSyncMode(line, property);
                return;
            }

            NextLine(ref line);
            EditorGUI.PropertyField(
                line,
                property.FindPropertyRelative(nameof(ExtraParameterEntry.parameterName)),
                G("prop.extra_parameter.parameter_name"));

            NextLine(ref line);
            EditorGUI.PropertyField(
                line,
                property.FindPropertyRelative(nameof(ExtraParameterEntry.parameterType)),
                G("prop.extra_parameter.parameter_type"));

            NextLine(ref line);
            EditorGUI.PropertyField(
                line,
                property.FindPropertyRelative(nameof(ExtraParameterEntry.engagedValue)),
                G("prop.extra_parameter.engaged_value"));

            NextLine(ref line);
            DrawReleaseMode(line, property);

            if (IsRevert(property))
            {
                NextLine(ref line);
                EditorGUI.PropertyField(
                    line,
                    property.FindPropertyRelative(nameof(ExtraParameterEntry.releasedValue)),
                    G("prop.extra_parameter.released_value"));
            }

            NextLine(ref line);
            DrawSyncMode(line, property);
        }

        private static void DrawSyncMode(Rect line, SerializedProperty property)
        {
            DrawLocalizedEnumPopup(
                line,
                property.FindPropertyRelative(nameof(ExtraParameterEntry.syncMode)),
                "prop.extra_parameter.sync_mode",
                SyncModeLabelKeys);
        }

        private static void DrawReleaseMode(Rect line, SerializedProperty property)
        {
            var content = G("prop.extra_parameter.release_mode");

            // メニューアイテムの「解除時の値を書く」は、書き込む値が入力欄に出ない。
            // 何を書くのかをツールチップで補う
            var source = property.FindPropertyRelative(nameof(ExtraParameterEntry.source));
            if (source.enumValueIndex == (int)ExtraParameterSource.MenuItem)
            {
                content.tooltip = S("inspector.extra_parameter.menu_item_revert");
            }

            DrawLocalizedEnumPopup(
                line,
                property.FindPropertyRelative(nameof(ExtraParameterEntry.releaseMode)),
                content,
                ReleaseModeLabelKeys);
        }

        private static bool IsRevert(SerializedProperty property)
        {
            return property.FindPropertyRelative(nameof(ExtraParameterEntry.releaseMode)).enumValueIndex
                   == (int)ExtraReleaseMode.Revert;
        }

        private static void NextLine(ref Rect line)
        {
            line.y += LineHeight + Spacing;
        }

        private static void DrawLocalizedEnumPopup(
            Rect rect, SerializedProperty property, string labelKey, IReadOnlyList<string> optionKeys)
        {
            DrawLocalizedEnumPopup(rect, property, G(labelKey), optionKeys);
        }

        /// <summary>
        /// 訳語を当てたenumのポップアップを描く
        /// BeginPropertyで囲み、プレハブの上書き表示とRevertを通常のフィールドと揃える
        /// </summary>
        private static void DrawLocalizedEnumPopup(
            Rect rect, SerializedProperty property, GUIContent labelContent, IReadOnlyList<string> optionKeys)
        {
            var options = new GUIContent[optionKeys.Count];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = G(optionKeys[i]);
            }

            var label = EditorGUI.BeginProperty(rect, labelContent, property);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            var selected = EditorGUI.Popup(rect, label, property.enumValueIndex, options);

            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = selected;
            }

            EditorGUI.EndProperty();
        }
    }
}
