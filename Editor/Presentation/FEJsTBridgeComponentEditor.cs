using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf.ui;
using FEJsTBridge.Domain;
using FEJsTBridge.Infra;
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

        private void OnEnable()
        {
            // 更新の確認は応答が返った時点で結果が変わる。
            // インスペクタが操作されるまで古い表示のままにしない
            UpdateCheck.ResultChanged += Repaint;
        }

        private void OnDisable()
        {
            UpdateCheck.ResultChanged -= Repaint;
            _inspection = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            LanguageSwitcher.DrawImmediate();
            EditorGUILayout.LabelField("Kx VRC FE-JsT Bridge", EditorStyles.boldLabel);

            // 更新の案内はヘッダーの直下へ置く。読み飛ばされない位置で、設定の並びは崩さない
            UpdateNotice.Draw();

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
            // 追加先のserializedObjectは選択中の全コンポーネントを指す。
            // 別のアバターの候補名まで書き込まないよう、複数選択中は調べない
            if (targets.Length > 1)
            {
                _inspection = null;
                EditorGUILayout.HelpBox(S("inspector.inspect.multi_edit"), MessageType.Info);
                return;
            }

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

            DrawReferenceWarnings();

            // 除去は名前で行うため、同名のレイヤーは一つの候補にまとめる
            var groups = _inspection.Report.Candidates
                .GroupBy(candidate => FxLayerRemovalPlan.NormalizeName(candidate.LayerName))
                .Select(group => group.ToArray())
                .ToArray();

            if (groups.Length == 0)
            {
                EditorGUILayout.HelpBox(S("inspector.inspect.no_candidate"), MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField(S("inspector.inspect.candidates"), EditorStyles.boldLabel);

                _inspectionScroll = EditorGUILayout.BeginScrollView(
                    _inspectionScroll, GUILayout.MaxHeight(220f));
                foreach (var group in groups)
                {
                    DrawCandidate(group);
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button(S("inspector.inspect.add_all")))
                {
                    foreach (var group in groups)
                    {
                        AddLayerName(group[0].LayerName);
                    }
                }
            }

            DrawOtherLayers();
        }

        /// <summary>
        /// 比較の基準が欠けている場合に断る
        /// 欠けた側とだけ競合するレイヤーは、競合なしと出てしまう
        /// </summary>
        private void DrawReferenceWarnings()
        {
            if (!_inspection.Report.HasReference)
            {
                // 見つかっているのに基準が空なら、推測ではなく設定を見直してもらう
                var message = _inspection.FaceEmoFound || _inspection.JerryFound
                    ? "inspector.inspect.empty_reference"
                    : "inspector.inspect.no_reference";
                EditorGUILayout.HelpBox(S(message), MessageType.Warning);
            }
            else
            {
                if (!_inspection.FaceEmoFound)
                {
                    EditorGUILayout.HelpBox(S("inspector.inspect.no_face_emo"), MessageType.Warning);
                }

                if (!_inspection.JerryFound)
                {
                    EditorGUILayout.HelpBox(S("inspector.inspect.no_jerry"), MessageType.Warning);
                }
            }

            // 比較対象の有無にかかわらず、判定できていないものがあることは伝える
            if (_inspection.Report.HasUnjudgedWriteDefaults)
            {
                EditorGUILayout.HelpBox(S("inspector.inspect.write_defaults"), MessageType.Info);
            }
        }

        /// <summary>
        /// 同じ名前の候補を一件として描く
        /// </summary>
        private void DrawCandidate(IReadOnlyList<FxLayerConflict> group)
        {
            var layerName = FxLayerRemovalPlan.NormalizeName(group[0].LayerName);
            var canAdd = !string.IsNullOrEmpty(layerName);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        S("inspector.inspect.layer", layerName, Indices(group.Select(item => item.LayerIndex))));

                    // 名前で除去するため、名前の無いレイヤーは指定できない
                    using (new EditorGUI.DisabledScope(!canAdd))
                    {
                        if (GUILayout.Button(S("inspector.inspect.add"), GUILayout.Width(60f)) && canAdd)
                        {
                            AddLayerName(layerName);
                        }
                    }
                }

                if (!canAdd)
                {
                    EditorGUILayout.HelpBox(S("inspector.inspect.empty_name"), MessageType.Warning);
                }

                foreach (var candidate in group)
                {
                    // 同名が複数あるときは、どのレイヤーの根拠かを添える
                    if (group.Count > 1)
                    {
                        EditorGUILayout.LabelField(
                            S("inspector.inspect.layer_index", candidate.LayerIndex), EditorStyles.miniLabel);
                    }

                    foreach (var reason in DescribeReasons(candidate))
                    {
                        EditorGUILayout.LabelField(reason, EditorStyles.miniLabel);
                    }
                }

                DrawSameNameWarning(layerName);
            }
        }

        /// <summary>
        /// 候補と同じ名前で、競合しないレイヤーがある場合に断る
        /// 除去は名前で行うため、追加するとそれらもまとめて消える
        /// </summary>
        private void DrawSameNameWarning(string layerName)
        {
            var others = _inspection.Report.Layers
                .Where(layer => FxLayerRemovalPlan.NormalizeName(layer.LayerName) == layerName
                    && layer.Verdict != FxLayerVerdict.Candidate)
                .ToArray();
            if (others.Length == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                S("inspector.inspect.same_name", others.Length, Indices(others.Select(layer => layer.LayerIndex))),
                MessageType.Warning);
        }

        private static string Indices(IEnumerable<int> indices)
        {
            return string.Join(", ", indices.Select(index => index.ToString()));
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
            // 除去時と同じ規則でそろえてから入れる
            var name = FxLayerRemovalPlan.NormalizeName(layerName);

            // 空名は除去時に落とされる。入れても消えないものを一覧へ残さない
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var property = serializedObject.FindProperty("removeFxLayers");

            for (var i = 0; i < property.arraySize; i++)
            {
                if (FxLayerRemovalPlan.NormalizeName(
                        property.GetArrayElementAtIndex(i).stringValue) == name)
                {
                    return;
                }
            }

            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = name;
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
