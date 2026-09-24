namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 追加パラメータを同期パラメータとして扱うかを決める
    /// </summary>
    internal static class ExtraParameterSync
    {
        /// <param name="mode">項目ごとの設定</param>
        /// <param name="declaredSynced">
        /// Expression ParametersかMA Parametersの宣言から読んだ同期の有無。宣言がなければnull
        /// </param>
        /// <param name="menuItemSynced">
        /// 宣言のないパラメータをMA Menu Itemが作るときの同期の有無。作るメニューアイテムがなければnull
        /// </param>
        /// <remarks>
        /// どこにも宣言がなく、メニューアイテムも作らないパラメータは、
        /// Expression Parametersに載らないアニメーターだけのパラメータなので、同期しない。
        ///
        /// 判定を誤った場合の影響は非対称である。
        /// 同期パラメータを同期しないものとして扱うと、リモートでも書き込み、届いた同期値と一時的に競合する。
        /// 同期しないパラメータを同期するものとして扱うと、リモートでは一切切り替わらない。
        /// 分からないものを同期しない側へ倒すのは、後者を避けるためである。
        /// </remarks>
        public static bool Resolve(ExtraSyncMode mode, bool? declaredSynced, bool? menuItemSynced)
        {
            switch (mode)
            {
                case ExtraSyncMode.Synced:
                    return true;
                case ExtraSyncMode.Unsynced:
                    return false;
                default:
                    return declaredSynced ?? menuItemSynced ?? false;
            }
        }
    }
}
