using UnityEngine;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// フェイストラッキング有効中に追加で書き込むパラメータのうち、名前と値を解決し終えたもの
    ///
    /// 名前はMAのリネームを反映したあとの、FXで使われる名前を持つ。
    /// 生成計画はこの値をそのまま写すだけで、メニューアイテムやMA Parametersは参照しない。
    /// </summary>
    internal sealed class ExtraParameterTarget
    {
        public ExtraParameterTarget(
            string name, BridgeParameterType type, float engagedValue, float? releasedValue)
        {
            Name = name;
            Type = type;
            EngagedValue = NormalizeValue(type, engagedValue);
            ReleasedValue = releasedValue.HasValue ? NormalizeValue(type, releasedValue.Value) : (float?)null;
        }

        public string Name { get; }

        public BridgeParameterType Type { get; }

        /// <summary>トリガーが立ったときに書く値</summary>
        public float EngagedValue { get; }

        /// <summary>トリガーが下りたときに書く値。nullなら何も書かない</summary>
        public float? ReleasedValue { get; }

        /// <summary>
        /// 直接指定の設定から組み立てる
        /// </summary>
        public static ExtraParameterTarget FromDirect(
            string name,
            ExtraParameterType type,
            float engagedValue,
            ExtraReleaseMode releaseMode,
            float releasedValue)
        {
            return new ExtraParameterTarget(
                name,
                ToBridgeType(type),
                engagedValue,
                releaseMode == ExtraReleaseMode.Revert ? releasedValue : (float?)null);
        }

        /// <summary>
        /// メニューアイテムの設定から組み立てる
        ///
        /// ONはトグルの値を、OFFは0を書く。VRChatのトグルは、選ばれたときに自分の値を書き、
        /// 選択が外れたときに0を書くので、それと同じ値になる。
        /// 解除時は、フェイストラッキング中と反対の状態へ戻す。
        /// </summary>
        public static ExtraParameterTarget FromMenuItem(
            string name,
            BridgeParameterType type,
            float toggleValue,
            ExtraToggleState engagedState,
            ExtraReleaseMode releaseMode)
        {
            var onValue = toggleValue;
            const float offValue = 0f;

            var engagedValue = engagedState == ExtraToggleState.On ? onValue : offValue;
            var releasedValue = engagedState == ExtraToggleState.On ? offValue : onValue;

            return new ExtraParameterTarget(
                name,
                type,
                engagedValue,
                releaseMode == ExtraReleaseMode.Revert ? releasedValue : (float?)null);
        }

        /// <summary>
        /// 型に合わない値を、Driverに渡せる値へ揃える
        /// Boolは0以外を1へ、Intは最も近い整数へ丸める
        /// </summary>
        public static float NormalizeValue(BridgeParameterType type, float value)
        {
            switch (type)
            {
                case BridgeParameterType.Bool:
                    return Mathf.Approximately(value, 0f) ? 0f : 1f;
                case BridgeParameterType.Int:
                    return Mathf.Round(value);
                default:
                    return value;
            }
        }

        public static BridgeParameterType ToBridgeType(ExtraParameterType type)
        {
            switch (type)
            {
                case ExtraParameterType.Int:
                    return BridgeParameterType.Int;
                case ExtraParameterType.Float:
                    return BridgeParameterType.Float;
                default:
                    return BridgeParameterType.Bool;
            }
        }
    }
}
