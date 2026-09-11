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
            ControlMethod controlMethod,
            BypassTrigger bypassTrigger,
            bool enableTrackingReapply,
            float reapplyDelaySeconds,
            int faceEmoteIndex)
        {
            ControlMethod = controlMethod;
            BypassTrigger = bypassTrigger;
            EnableTrackingReapply = enableTrackingReapply;

            // クリップ長として使うため、スクリプトから範囲外の値を入れられても破綻しないよう丸める
            ReapplyDelaySeconds = Mathf.Clamp(
                reapplyDelaySeconds,
                FEJsTBridgeComponent.MinReapplyDelaySeconds,
                FEJsTBridgeComponent.MaxReapplyDelaySeconds);

            // FaceEmoの表情番号は0から振られるため、負の値はDriverへ渡さない
            FaceEmoteIndex = Mathf.Max(0, faceEmoteIndex);
        }

        public ControlMethod ControlMethod { get; }

        public BypassTrigger BypassTrigger { get; }

        public bool EnableTrackingReapply { get; }

        public float ReapplyDelaySeconds { get; }

        /// <summary>表情制御方式で切り替え先にする表情番号</summary>
        public int FaceEmoteIndex { get; }

        public static BridgeSettings Default =>
            new BridgeSettings(
                ControlMethod.ExpressionControl,
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
                component.controlMethod,
                component.bypassTrigger,
                component.enableTrackingReapply,
                component.reapplyDelaySeconds,
                component.faceEmoteIndex);
        }
    }
}
