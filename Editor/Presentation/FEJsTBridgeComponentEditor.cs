using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf.ui;
using FEJsTBridge.Domain;
using FEJsTBridge.UseCase;
using static FEJsTBridge.Localization;

namespace FEJsTBridge.Presentation
{
    /// <summary>
    /// FEJsTBridgeComponentのインスペクタ
    ///
    /// 表示文言はNDMFのローカライズ機構から引く。
    /// enumの選択肢はPropertyFieldではC#の識別子がそのまま出てしまうため、
    /// ポップアップを自前で描いて訳語を当てる。
    /// 自前で描いた分はBeginProperty/EndPropertyで囲み、プレハブの上書き表示を保つ。
    /// </summary>
    [CustomEditor(typeof(FEJsTBridgeComponent))]
    [CanEditMultipleObjects]
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

        /// <summary>調査の結果。対象が変わるまで保持する</summary>
        private FxLayerInspection _inspection;

        private Vector2 _inspectionScroll;
        private bool _showOtherLayers;

        private void OnDisable()
        {
            _inspection = null;
        }

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

            EditorGUILayout.Space();

            DrawRemoveFxLayers();

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

            // BeginPropertyで囲むと、プレハブインスタンス上での上書き表示と
            // 右クリックのRevertが、通常のフィールドと同じように働く
            var rect = EditorGUILayout.GetControlRect();
            var label = EditorGUI.BeginProperty(rect, G("prop.bypass_trigger"), property);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            // enumValueIndexは宣言順の添字なので、選択肢の並びと対応する
            var selected = EditorGUI.Popup(rect, label, property.enumValueIndex, options);

            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = selected;
            }

            EditorGUI.EndProperty();

            if (!property.hasMultipleDifferentValues
                && property.enumValueIndex == (int)BypassTrigger.LipTrackingOnly)
            {
                EditorGUILayout.HelpBox(S("inspector.bypass_trigger.lip_tracking_only"), MessageType.Warning);
            }
        }

        private void DrawRemoveFxLayers()
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("removeFxLayers"), G("prop.remove_fx_layers"), true);
            EditorGUILayout.HelpBox(S("inspector.remove_fx_layers"), MessageType.Info);

            DrawInspection();
        }

        /// <summary>
        /// 除去候補の調査
        /// レイヤー名から推測させるかわりに、何を書くレイヤーなのかを根拠として出す
        /// </summary>
        private void DrawInspection()
        {
            if (GUILayout.Button(S("inspector.inspect.button")))
            {
                _inspection = InspectFxLayersUseCase.Inspect(FindAvatarRoot());
                _showOtherLayers = false;
            }

            if (_inspection == null)
            {
                return;
            }

            if (!_inspection.FxFound)
            {
                EditorGUILayout.HelpBox(S("inspector.inspect.no_fx"), MessageType.Warning);
                return;
            }

            if (!_inspection.Report.HasReference)
            {
                EditorGUILayout.HelpBox(S("inspector.inspect.no_reference"), MessageType.Warning);
            }

            var candidates = _inspection.Report.Candidates.ToArray();
            if (candidates.Length == 0)
            {
                EditorGUILayout.HelpBox(S("inspector.inspect.no_candidate"), MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField(S("inspector.inspect.candidates"), EditorStyles.boldLabel);

                _inspectionScroll = EditorGUILayout.BeginScrollView(
                    _inspectionScroll, GUILayout.MaxHeight(220f));
                foreach (var candidate in candidates)
                {
                    DrawCandidate(candidate);
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button(S("inspector.inspect.add_all")))
                {
                    foreach (var candidate in candidates)
                    {
                        AddLayerName(candidate.LayerName);
                    }
                }
            }

            DrawOtherLayers();
        }

        private void DrawCandidate(FxLayerConflict candidate)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        S("inspector.inspect.layer", candidate.LayerName, candidate.LayerIndex));
                    if (GUILayout.Button(S("inspector.inspect.add"), GUILayout.Width(60f)))
                    {
                        AddLayerName(candidate.LayerName);
                    }
                }

                foreach (var reason in DescribeReasons(candidate))
                {
                    EditorGUILayout.LabelField(reason, EditorStyles.miniLabel);
                }
            }
        }

        private IEnumerable<string> DescribeReasons(FxLayerConflict candidate)
        {
            if (candidate.SharedCount > 0)
            {
                var samples = string.Join(", ", candidate.SharedShapeNames);
                yield return _inspection.Report.HasReference
                    ? S("inspector.inspect.reason.shared", candidate.SharedCount, samples)
                    : S("inspector.inspect.reason.blend_shapes", candidate.SharedCount, samples);
            }

            if (candidate.ChangesTrackingControl)
            {
                yield return S("inspector.inspect.reason.tracking_control");
            }
        }

        private void DrawOtherLayers()
        {
            var others = _inspection.Report.Layers
                .Where(layer => layer.Verdict != FxLayerVerdict.Candidate)
                .ToArray();
            if (others.Length == 0)
            {
                return;
            }

            _showOtherLayers = EditorGUILayout.Foldout(
                _showOtherLayers, S("inspector.inspect.others", others.Length), true);
            if (!_showOtherLayers)
            {
                return;
            }

            EditorGUI.indentLevel++;
            foreach (var layer in others)
            {
                var verdict = layer.Verdict == FxLayerVerdict.Managed
                    ? S("inspector.inspect.verdict.managed")
                    : S("inspector.inspect.verdict.no_conflict");
                EditorGUILayout.LabelField(
                    S("inspector.inspect.layer", layer.LayerName, layer.LayerIndex), verdict);
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 一覧へ名前を足す。すでに入っていれば何もしない
        /// </summary>
        private void AddLayerName(string layerName)
        {
            var property = serializedObject.FindProperty("removeFxLayers");

            for (var i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return;
                }
            }

            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = layerName;
        }

        /// <summary>
        /// コンポーネントの位置にかかわらず、アバターのルートを見つける
        /// </summary>
        private GameObject FindAvatarRoot()
        {
            var component = (FEJsTBridgeComponent)target;
            var descriptor = component.GetComponentInParent<VRCAvatarDescriptor>(true);

            return descriptor != null ? descriptor.gameObject : component.transform.root.gameObject;
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
