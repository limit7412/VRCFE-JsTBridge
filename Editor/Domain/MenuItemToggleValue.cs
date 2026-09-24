using UnityEngine;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// MA Menu Itemのトグル値と型を、MAがビルド時に決めるのと同じ規則で求める
    ///
    /// ブリッジはMAより前に動くため、MAが割り当てる値をまだ読めない。
    /// 同じ規則をここでなぞり、MAの割り当てと一致する値を先に求める。
    /// 規則はModular Avatar 1.10のParameterAssignerPassに合わせている。
    /// </summary>
    internal static class MenuItemToggleValue
    {
        /// <summary>
        /// トグル値からパラメータの型を決める
        /// 0と1はBool、2以上の整数はInt、負の値と端数を持つ値はFloatになる
        /// </summary>
        public static BridgeParameterType TypeOf(float value)
        {
            if (value < 0f || Mathf.Abs(value - Mathf.Round(value)) > 0.01f)
            {
                return BridgeParameterType.Float;
            }

            return value > 1f ? BridgeParameterType.Int : BridgeParameterType.Bool;
        }

        /// <summary>
        /// 値の自動割り当て (automaticValue) が有効なメニューアイテムの値を求める
        /// </summary>
        /// <param name="isDefault">メニューアイテムの「既定値にする」設定</param>
        /// <param name="itemCount">同じパラメータを参照するメニューアイテムの数 (自分を含む)</param>
        /// <param name="declaredDefault">
        /// Expression ParametersかMA Parametersで宣言済みのときの既定値。未宣言ならnull
        /// </param>
        /// <returns>求まった値。求められない場合はnull</returns>
        /// <remarks>
        /// 同じパラメータを複数のメニューアイテムが参照する場合、MAは階層順に値を振っていく。
        /// その順序はビルド時のヒエラルキーで決まり、なぞると不一致を起こしやすいため扱わない。
        /// 単独のトグルならMAの割り当ては次のように決まる。
        /// 未宣言なら、既定値かどうかにかかわらず1になる。
        /// 宣言済みで既定値にするなら、宣言の既定値になる。
        /// 宣言済みで既定値にしないなら、宣言の既定値を避けて1から探した値になる。
        /// </remarks>
        public static float? ResolveAutomatic(bool isDefault, int itemCount, float? declaredDefault)
        {
            if (itemCount != 1)
            {
                return null;
            }

            if (!declaredDefault.HasValue)
            {
                return 1f;
            }

            // MAは宣言の既定値を整数へ切り捨ててから使う
            var declared = (int)declaredDefault.Value;

            if (isDefault)
            {
                return declared;
            }

            return declared == 1 ? 2f : 1f;
        }
    }
}
