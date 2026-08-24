using UnityEngine;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 生成計画の入力となる設定値
    /// コンポーネントから切り離すことで、計画の構築をMonoBehaviourなしでテストできる
    /// </summary>
    internal readonly struct BridgeSettings
    {
        public BridgeSettings(BypassTrigger bypassTrigger, bool enableTrackingReapply, float reapplyDelaySeconds)
        {
            BypassTrigger = bypassTrigger;
            EnableTrackingReapply = enableTrackingReapply;

            // クリップ長として使うため、スクリプトから範囲外の値を入れられても破綻しないよう丸める
            ReapplyDelaySeconds = Mathf.Clamp(
                reapplyDelaySeconds,
                FEJsTBridgeComponent.MinReapplyDelaySeconds,
                FEJsTBridgeComponent.MaxReapplyDelaySeconds);
        }

        public BypassTrigger BypassTrigger { get; }

        public bool EnableTrackingReapply { get; }

        public float ReapplyDelaySeconds { get; }

        public static BridgeSettings Default =>
            new BridgeSettings(
                BypassTrigger.FacialExpressionsDisabled,
                true,
                FEJsTBridgeComponent.DefaultReapplyDelaySeconds);

        public static BridgeSettings FromComponent(FEJsTBridgeComponent component)
        {
            if (component == null)
            {
                return Default;
            }

            return new BridgeSettings(
                component.bypassTrigger,
                component.enableTrackingReapply,
                component.reapplyDelaySeconds);
        }
    }
}
