namespace FEJsTBridge.Domain
{
    /// <summary>
    /// ブリッジが読み書きするJerry's TemplatesとFaceEmoのパラメータ名
    ///
    /// いずれも相手パッケージの内部名であり、更新時の再検証が必要になる接点である。
    /// CN_FORCE_BYPASS_ENABLEはFaceEmoが外部連携用として維持を明示しているが、
    /// Jerry側の4つと、表情制御方式が使うFaceEmo側の3つには明示的な約束がない。
    /// </summary>
    internal static class BridgeParameterNames
    {
        /// <summary>目か口のトラッキングが有効なときJerryがtrueにする（同期あり）</summary>
        public const string FacialExpressionsDisabled = "FacialExpressionsDisabled";

        /// <summary>目のトラッキングの有効状態。animator上はFloatだが値は0か1しか取らない</summary>
        public const string EyeTrackingActive = "EyeTrackingActive";

        /// <summary>口のトラッキングの有効状態。animator上はFloatだが値は0か1しか取らない</summary>
        public const string LipTrackingActive = "LipTrackingActive";

        /// <summary>
        /// Jerryのメニューでリップシンクを切り替える同期パラメータ
        ///
        /// Jerry内部には非同期の写し（State/VisemesEnable）もあるが、写しは各クライアントの
        /// Driver実行で作られるため、アバターのロード中に失われると値が既定へ戻ったまま残る。
        /// 同期パラメータは値そのものが届き直すため、ブリッジはこちらを読む。
        /// </summary>
        public const string VisemesEnable = "VisemesEnable";

        /// <summary>FaceEmoのバイパスを起動する外部連携用パラメータ</summary>
        public const string ForceBypassEnable = "CN_FORCE_BYPASS_ENABLE";

        /// <summary>
        /// FaceEmoの表情ロック。メニューの「表情固定ON」が書き込むものと同じ
        ///
        /// trueの間はジェスチャーによる表情の切り替えが止まる。
        /// 以下の3つはFaceEmoのMA Parametersに登録されており、
        /// AddParameterPrefix設定が有効なアバターでは名前が変わる (FaceEmoParameterNames)。
        /// </summary>
        public const string EmoteLockEnable = "CN_EMOTE_LOCK_ENABLE";

        /// <summary>
        /// FaceEmoのまばたき強制停止。メニューの「まばたきOFF」が書き込むものと同じ
        ///
        /// BLINKレイヤーはこれがtrueなら無条件で停止側へ遷移するため、
        /// 表情ステートがまばたきの有効を駆動し直しても止まったままになる。
        /// </summary>
        public const string ForceBlinkDisable = "SYNC_CN_FORCE_BLINK_DISABLE";

        /// <summary>
        /// FaceEmoの再生中の表情番号。メニューの「表情選択」が書き込むものと同じ
        ///
        /// FaceEmo側でこの値を書き直すのはジェスチャーが変化した瞬間だけなので、
        /// 外から書いた値は次の変化まで保たれる。表情ロック中はその変化自体が起きない。
        /// </summary>
        public const string Emote = "SYNC_EM_EMOTE";
    }
}
