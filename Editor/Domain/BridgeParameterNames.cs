namespace FEJsTBridge.Domain
{
    /// <summary>
    /// ブリッジが読み書きするJerry's TemplatesとFaceEmoのパラメータ名
    ///
    /// いずれも相手パッケージの内部名であり、更新時の再検証が必要になる接点である。
    /// CN_FORCE_BYPASS_ENABLEはFaceEmoが外部連携用として維持を明示しているが、
    /// Jerry側の4つには明示的な約束がない。
    /// </summary>
    internal static class BridgeParameterNames
    {
        /// <summary>目か口のトラッキングが有効なときJerryがtrueにする（同期あり）</summary>
        public const string FacialExpressionsDisabled = "FacialExpressionsDisabled";

        /// <summary>目のトラッキングの有効状態。animator上はFloatだが値は0か1しか取らない</summary>
        public const string EyeTrackingActive = "EyeTrackingActive";

        /// <summary>口のトラッキングの有効状態。animator上はFloatだが値は0か1しか取らない</summary>
        public const string LipTrackingActive = "LipTrackingActive";

        /// <summary>Jerryがリップシンクの有効状態を持つ非同期パラメータ</summary>
        public const string VisemesEnable = "State/VisemesEnable";

        /// <summary>FaceEmoのバイパスを起動する外部連携用パラメータ</summary>
        public const string ForceBypassEnable = "CN_FORCE_BYPASS_ENABLE";
    }
}
