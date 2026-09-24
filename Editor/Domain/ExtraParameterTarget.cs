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
        /// <param name="name">FXで使われる名前</param>
        /// <param name="type">パラメータの型</param>
        /// <param name="engagedValue">トリガーが立ったときに書く値</param>
        /// <param name="releasedValue">トリガーが下りたときに書く値。restoreがtrueのときは無視する</param>
        /// <param name="restore">トリガーが下りたときに、立つ直前の値へ戻すか</param>
        /// <param name="synced">同期パラメータか</param>
        public ExtraParameterTarget(
            string name,
            BridgeParameterType type,
            float engagedValue,
            float? releasedValue,
            bool restore = false,
            bool synced = true)
        {
            Name = name;
            Type = type;
            EngagedValue = NormalizeValue(type, engagedValue);
            Restore = restore;
            ReleasedValue = !restore && releasedValue.HasValue
                ? NormalizeValue(type, releasedValue.Value)
                : (float?)null;
            Synced = synced;
        }

        public string Name { get; }

        public BridgeParameterType Type { get; }

        /// <summary>トリガーが立ったときに書く値</summary>
        public float EngagedValue { get; }

        /// <summary>
        /// トリガーが下りたときに書く値
        /// nullなら値は書かない。Restoreがtrueのときは、値の代わりに退避しておいた値を書き戻す
        /// </summary>
        public float? ReleasedValue { get; }

        /// <summary>トリガーが立つ直前の値を退避し、下りたときに書き戻すか</summary>
        public bool Restore { get; }

        /// <summary>
        /// 同期パラメータか
        /// 同期パラメータは装着者のクライアントだけで書き、同期しないパラメータは各クライアントで書く
        /// </summary>
        public bool Synced { get; }

        /// <summary>
        /// 退避先のパラメータ名
        /// ブリッジのコントローラにだけ宣言し、Expression Parametersには載せない
        /// </summary>
        public string StashName => StashPrefix + Name;

        /// <summary>
        /// 退避先の名前の接頭辞
        /// この接頭辞で始まる名前はブリッジの予約名であり、追加パラメータの書き込み先には使えない。
        /// 使えると、ある項目の退避先と別の項目の書き込み先が同じ名前になり、退避した値が上書きされる
        /// </summary>
        public const string StashPrefix = "FEJsTBridge/Stash/";

        public static bool IsReservedName(string name)
        {
            return name != null && name.StartsWith(StashPrefix, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 直接指定の設定から組み立てる
        /// </summary>
        public static ExtraParameterTarget FromDirect(
            string name,
            ExtraParameterType type,
            float engagedValue,
            ExtraReleaseMode releaseMode,
            float releasedValue,
            bool synced = true)
        {
            return new ExtraParameterTarget(
                name,
                ToBridgeType(type),
                engagedValue,
                releaseMode == ExtraReleaseMode.Revert ? releasedValue : (float?)null,
                releaseMode == ExtraReleaseMode.Restore,
                synced);
        }

        /// <summary>
        /// メニューアイテムの設定から組み立てる
        ///
        /// ONはトグルの値を、OFFは0を書く。VRChatのトグルは、選ばれたときに自分の値を書き、
        /// 選択が外れたときに0を書くので、それと同じ値になる。
        /// 解除時の値を書く設定 (Revert) では、フェイストラッキング中と反対の状態へ戻す。
        /// </summary>
        public static ExtraParameterTarget FromMenuItem(
            string name,
            BridgeParameterType type,
            float toggleValue,
            ExtraToggleState engagedState,
            ExtraReleaseMode releaseMode,
            bool synced = true)
        {
            var onValue = toggleValue;
            const float offValue = 0f;

            var engagedValue = engagedState == ExtraToggleState.On ? onValue : offValue;
            var releasedValue = engagedState == ExtraToggleState.On ? offValue : onValue;

            return new ExtraParameterTarget(
                name,
                type,
                engagedValue,
                releaseMode == ExtraReleaseMode.Revert ? releasedValue : (float?)null,
                releaseMode == ExtraReleaseMode.Restore,
                synced);
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
