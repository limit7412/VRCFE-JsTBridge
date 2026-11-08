using UnityEditor;
using UnityEngine;

namespace FEJsTBridge.Handler
{
    /// <summary>
    /// ヒエラルキーの右クリック（GameObjectメニュー）から、FEJsTBridgeComponentを付けたオブジェクトを作る
    ///
    /// コンポーネントはアバタールートに限らず、アバター内のどこに置いても動く。
    /// ビルドはアバター全体からコンポーネントを探し、生成物はアバタールートを基準に置くためである。
    /// 専用のオブジェクトに分けておけば、アバタールートのコンポーネントの並びを崩さずに追加と削除ができる。
    ///
    /// プレハブは配らない。中身はコンポーネント1つだけなので、プレハブにすると
    /// 版を上げるたびに既定値をプレハブへ書き直す手間が増えるうえ、利用者の設定がオーバーライドとして残る。
    /// ここで作れば、既定値はコンポーネントのフィールドの初期化子の1か所で決まる。
    /// </summary>
    internal static class CreateBridgeMenu
    {
        private const string MenuRoot = "GameObject/Kx VRC FE-JsT Bridge/";

        public const string ObjectName = "Kx FE-JsT Bridge";

        // 10から49の範囲はヒエラルキーの右クリックにも出る
        private const int Priority = 10;

        [MenuItem(MenuRoot + ObjectName, false, Priority)]
        private static void Create(MenuCommand command)
        {
            CreateObject(command.context as GameObject);
        }

        /// <summary>
        /// parentの子にコンポーネント付きのオブジェクトを作る。parentがnullならシーンの直下に作る
        /// </summary>
        internal static GameObject CreateObject(GameObject parent)
        {
            var gameObject = new GameObject(ObjectName);

            // 右クリックしたオブジェクトの子にする。複数選択では選択ごとに呼ばれ、それぞれの子に作られる
            GameObjectUtility.SetParentAndAlign(gameObject, parent);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + ObjectName);

            // AddComponentでフィールドの初期化子の値が入る。
            // アバターにすでにコンポーネントがあれば、重複の排除がどちらを残すかを決める
            Undo.AddComponent<FEJsTBridgeComponent>(gameObject);

            Selection.activeGameObject = gameObject;
            return gameObject;
        }
    }
}
