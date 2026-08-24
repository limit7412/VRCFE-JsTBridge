using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf.ui;
using static FEJsTBridge.Localization;

namespace FEJsTBridge.Presentation
{
    /// <summary>
    /// FEJsTBridgeComponentのインスペクタ
    ///
    /// 表示文言はNDMFのローカライズ機構から引く。
    /// enumの選択肢はPropertyFieldではC#の識別子がそのまま出てしまうため、
    /// ポップアップを自前で描いて訳語を当てる。
    /// </summary>
    [CustomEditor(typeof(FEJsTBridgeComponent))]
    public class FEJsTBridgeComponentEditor : Editor
    {
        /// <summary>
        /// バイパス発動条件の選択肢の文言キー
        /// 並びはBypassTriggerの宣言順に一致させる (enumValueIndexで引くため)
        /// </summary>
        internal static readonly IReadOnlyList<string> BypassTriggerLabelKeys = new[]
        {
            "prop.bypass_trigger.facial_expressions_disabled",
            "prop.bypass_trigger.lip_tracking_only",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            LanguageSwitcher.DrawImmediate();
            EditorGUILayout.LabelField("Kx VRC FE-JsT Bridge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(S("inspector.description"), MessageType.Info);

            EditorGUILayout.Space();

            DrawBypassTrigger();

            EditorGUILayout.Space();

            DrawTrackingReapply();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBypassTrigger()
        {
            var property = serializedObject.FindProperty("bypassTrigger");

            var options = new GUIContent[BypassTriggerLabelKeys.Count];
            for (var i = 0; i < options.Length; i++)
            {
                options[i] = G(BypassTriggerLabelKeys[i]);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            // enumValueIndexは宣言順の添字なので、選択肢の並びと対応する
            var selected = EditorGUILayout.Popup(G("prop.bypass_trigger"), property.enumValueIndex, options);

            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = selected;
            }

            if (!property.hasMultipleDifferentValues
                && property.enumValueIndex == (int)BypassTrigger.LipTrackingOnly)
            {
                EditorGUILayout.HelpBox(S("inspector.bypass_trigger.lip_tracking_only"), MessageType.Warning);
            }
        }

        private void DrawTrackingReapply()
        {
            var enableProperty = serializedObject.FindProperty("enableTrackingReapply");
            EditorGUILayout.PropertyField(enableProperty, G("prop.enable_tracking_reapply"));

            if (!enableProperty.hasMultipleDifferentValues && !enableProperty.boolValue)
            {
                EditorGUILayout.HelpBox(S("inspector.tracking_reapply.disabled"), MessageType.Warning);
            }

            // 待ち時間は再適用レイヤーのクリップ長にしか効かない
            using (new EditorGUI.DisabledScope(
                !enableProperty.hasMultipleDifferentValues && !enableProperty.boolValue))
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("reapplyDelaySeconds"), G("prop.reapply_delay_seconds"));
            }
        }
    }
}
