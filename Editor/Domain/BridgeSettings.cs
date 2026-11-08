using System.Collections.Generic;
using UnityEngine;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 生成計画の入力となる設定値
    /// コンポーネントから切り離すことで、計画の構築をMonoBehaviourなしでテストできる
    /// </summary>
    internal readonly struct BridgeSettings
    {
        public BridgeSettings(
            BypassTrigger bypassTrigger,
            bool enableTrackingReapply,
            float reapplyDelaySeconds,
            int faceEmoteIndex,
            IReadOnlyList<ExtraParameterTarget> extraParameters = null)
        {
            BypassTrigger = bypassTrigger;
            EnableTrackingReapply = enableTrackingReapply;

            // クリップ長として使うため、スクリプトから範囲外の値を入れられても破綻しないよう丸める
            ReapplyDelaySeconds = Mathf.Clamp(
                reapplyDelaySeconds,
                FEJsTBridgeComponent.MinReapplyDelaySeconds,
                FEJsTBridgeComponent.MaxReapplyDelaySeconds);

            // FaceEmoの表情番号は0から振られるため、負の値はDriverへ渡さない
            FaceEmoteIndex = Mathf.Max(0, faceEmoteIndex);

            ExtraParameters = extraParameters ?? new ExtraParameterTarget[0];
        }

        public BypassTrigger BypassTrigger { get; }

        public bool EnableTrackingReapply { get; }

        public float ReapplyDelaySeconds { get; }

        /// <summary>フェイストラッキング中に切り替え先にする表情番号</summary>
        public int FaceEmoteIndex { get; }

        /// <summary>
        /// フェイストラッキング有効中に追加で書き込むパラメータ
        /// 名前の解決にはビルド中のアバターが要るため、FromComponentでは埋めずWithExtraParametersで渡す
        /// </summary>
        public IReadOnlyList<ExtraParameterTarget> ExtraParameters { get; }

        public BridgeSettings WithExtraParameters(IReadOnlyList<ExtraParameterTarget> extraParameters)
        {
            return new BridgeSettings(
                BypassTrigger,
                EnableTrackingReapply,
                ReapplyDelaySeconds,
                FaceEmoteIndex,
                extraParameters);
        }

        /// <summary>追加したてのコンポーネントと同じ設定</summary>
        public static BridgeSettings Default =>
            new BridgeSettings(
                BypassTrigger.FacialExpressionsDisabled,
                true,
                FEJsTBridgeComponent.DefaultReapplyDelaySeconds,
                FEJsTBridgeComponent.DefaultFaceEmoteIndex);

        public static BridgeSettings FromComponent(FEJsTBridgeComponent component)
        {
            if (component == null)
            {
                return Default;
            }

            return new BridgeSettings(
                component.bypassTrigger,
                component.enableTrackingReapply,
                component.reapplyDelaySeconds,
                component.faceEmoteIndex);
        }
    }
}
