using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
using nadena.dev.modular_avatar.core;

namespace FEJsTBridge
{
    /// <summary>
    /// FaceEmoとJerry's Templates (MA版) を橋渡しするコンポーネント
    /// フェイストラッキング有効中はFaceEmoの書き込みを止め、無効化したら元の動作へ戻す
    ///
    /// 保持するのは設定値だけで、実際の生成はNDMFのGenerating Phaseで行う。
    /// コンポーネント自体はビルド中に取り除かれる。
    ///
    /// 使用方法:
    /// 1. このコンポーネントをアバタールートに追加する
    /// 2. アップロード時にブリッジ用のアニメーターレイヤーが自動生成される
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("KxVRCFEJsTBridge/Kx VRC FE-JsT Bridge")]
    public class FEJsTBridgeComponent : MonoBehaviour, IEditorOnly
    {
        // インスペクタ表示用の文言はEditorアセンブリ側でローカライズされる
        // （以下の属性はカスタムエディタが無効な場合のフォールバック表示）
        [Tooltip("How to keep FaceEmo from writing while face tracking is active. ExpressionControl: keep FaceEmo running, and lock the expression, stop blinking, and switch to the chosen emote. Bypass: stop FaceEmo entirely, kept for backward compatibility")]
        public ControlMethod controlMethod = ControlMethod.Bypass;

        [Tooltip("Emote number to switch to while face tracking is active. It is the same number the FaceEmo expression select menu writes. Used only by ExpressionControl")]
        [Min(0)]
        public int faceEmoteIndex = DefaultFaceEmoteIndex;

        [Tooltip("Condition that triggers the bypass. FacialExpressionsDisabled: fires when either eye or lip tracking is active. LipTrackingOnly: fires only while lip tracking is active (experimental)")]
        public BypassTrigger bypassTrigger = BypassTrigger.FacialExpressionsDisabled;

        [Tooltip("Generate the layer that re-applies Tracking Control after the bypass takes effect. Turn it off only to work around trouble")]
        public bool enableTrackingReapply = true;

        [Tooltip("Seconds to wait for the bypass to take effect before re-applying Tracking Control")]
        [Range(MinReapplyDelaySeconds, MaxReapplyDelaySeconds)]
        public float reapplyDelaySeconds = DefaultReapplyDelaySeconds;

        [Tooltip("Names of FX layers to remove at build time. Use it for the avatar's own expression layers, which surface again while FaceEmo is bypassed. The avatar's own assets are not modified")]
        public List<string> removeFxLayers = new List<string>();

        [Tooltip("Parameters to set while face tracking is active, such as toggles made with MA Menu Item. Each entry is written when the trigger turns on, and written back when it turns off")]
        public List<ExtraParameterEntry> extraParameters = new List<ExtraParameterEntry>();

        /// <summary>
        /// 再適用の待ち時間の既定値
        /// Driverの連鎖は最大4フレーム程度（90fpsで約0.05秒）であり、それに余裕を乗せた値
        /// </summary>
        public const float DefaultReapplyDelaySeconds = 0.2f;

        public const float MinReapplyDelaySeconds = 0.05f;
        public const float MaxReapplyDelaySeconds = 1.0f;

        /// <summary>
        /// 切り替え先の表情番号の既定値
        /// FaceEmoは表情パターンの先頭から番号を振るため、0は最初の表情パターンのデフォルト表情を指す
        /// </summary>
        public const int DefaultFaceEmoteIndex = 0;

#if UNITY_EDITOR
        /// <summary>
        /// Editorアセンブリ側から差し込まれるOnValidateフック
        /// （同一アバター内の重複コンポーネント排除に使用）
        /// </summary>
        internal static Action<FEJsTBridgeComponent> EditorOnValidateHook;
#endif

        private void OnValidate()
        {
#if UNITY_EDITOR
            EditorOnValidateHook?.Invoke(this);
#endif
        }

        /// <summary>
        /// 追加したてのコンポーネントへ既定の制御方式を入れる
        /// </summary>
        /// <remarks>
        /// フィールドの初期化子に書かないのは、制御方式を持たない版で保存したアバターにまで
        /// 既定が渡ってしまうためである。Unityはシリアライズデータに無いフィールドを
        /// 初期化子の値のまま残すので、初期化子を表情制御にすると、更新しただけで
        /// バイパスで組んであったアバターの方式が変わる。
        /// 追加のときだけ呼ばれるResetで入れれば、古い保存データはバイパスのまま残る。
        /// </remarks>
        private void Reset()
        {
#if UNITY_EDITOR
            controlMethod = ControlMethod.ExpressionControl;
#endif
        }
    }

    /// <summary>
    /// FaceEmoの書き込みを止める方式
    ///
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない。
    /// 入れ替えると、追加済みのコンポーネントが別の方式で動くようになる。
    /// </summary>
    public enum ControlMethod
    {
        /// <summary>
        /// FaceEmoの外部連携用パラメータでバイパスさせ、FaceEmoごと止める
        ///
        /// 接点が1本で済み、FaceEmo側の設定にも依存しないが、
        /// FaceEmoが書き込みを止めた分だけ素体の表情レイヤーが表に出る。
        /// 表情制御方式より前からある方式であり、下位互換のために残している。
        /// </summary>
        [Tooltip("Stop FaceEmo entirely through its bypass parameter. Kept for backward compatibility")]
        Bypass,

        /// <summary>
        /// FaceEmoを動かしたまま、表情ロックとまばたき停止、表情の切り替えで無害な状態へ寄せる
        /// 素体の表情レイヤーは押さえ込まれたままになり、トラッキング中もメニューから表情を選べる
        /// </summary>
        [Tooltip("Keep FaceEmo running, and lock the expression, stop blinking, and switch to the chosen emote")]
        ExpressionControl
    }

    /// <summary>
    /// バイパスの発動条件
    /// </summary>
    public enum BypassTrigger
    {
        /// <summary>
        /// Jerry's TemplatesのFacialExpressionsDisabledに従う（目か口のどちらかが有効なら発動）
        /// </summary>
        [Tooltip("Follow FacialExpressionsDisabled (fires when either eye or lip tracking is active)")]
        FacialExpressionsDisabled,

        /// <summary>
        /// LipTrackingActiveに従う（口が有効なときだけ発動）
        /// 目だけのトラッキングではFaceEmoを止めないが、目系シェイプの競合は残る（実験的）
        /// </summary>
        [Tooltip("Follow LipTrackingActive (fires only while lip tracking is active). Experimental")]
        LipTrackingOnly
    }

    /// <summary>
    /// フェイストラッキング有効中に追加で書き込むパラメータ1件分の設定
    ///
    /// 書き込む先は、MA Menu Itemの参照か、パラメータ名の直接指定で決める。
    /// どちらの指定を使うかはsourceで選び、使わない側のフィールドは無視する。
    /// </summary>
    [Serializable]
    public class ExtraParameterEntry
    {
        [Tooltip("How to specify the parameter to write")]
        public ExtraParameterSource source = ExtraParameterSource.MenuItem;

        [Tooltip("MA Menu Item whose parameter is written. Its parameter name and toggle value are read at build time")]
        public ModularAvatarMenuItem menuItem;

        [Tooltip("State the menu item is switched to while face tracking is active")]
        public ExtraToggleState menuItemState = ExtraToggleState.Off;

        [Tooltip("Name of the parameter to write, as it appears in the FX layer after Modular Avatar renames it")]
        public string parameterName = "";

        [Tooltip("Type of the parameter")]
        public ExtraParameterType parameterType = ExtraParameterType.Bool;

        [Tooltip("Value written while face tracking is active")]
        public float engagedValue;

        [Tooltip("What to do when face tracking is turned off")]
        public ExtraReleaseMode releaseMode = ExtraReleaseMode.Restore;

        [Tooltip("Value written when face tracking is turned off. Used only when Release is Revert")]
        public float releasedValue = 1f;

        [Tooltip("Whether the parameter is synced over the network. Auto reads it from Expression Parameters, MA Parameters and MA Menu Item")]
        public ExtraSyncMode syncMode = ExtraSyncMode.Auto;
    }

    /// <summary>
    /// 追加パラメータの指定方法
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraParameterSource
    {
        [Tooltip("Read the parameter from an MA Menu Item")]
        MenuItem,

        [Tooltip("Write the parameter name directly")]
        ParameterName
    }

    /// <summary>
    /// メニューアイテムを切り替える先の状態
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraToggleState
    {
        [Tooltip("Turn the menu item on")]
        On,

        [Tooltip("Turn the menu item off")]
        Off
    }

    /// <summary>
    /// 直接指定したパラメータの型
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraParameterType
    {
        Bool,
        Int,
        Float
    }

    /// <summary>
    /// フェイストラッキングを無効化したときの扱い
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraReleaseMode
    {
        /// <summary>解除時の値を書き込む</summary>
        [Tooltip("Write the release value")]
        Revert,

        /// <summary>何も書き込まず、フェイストラッキング中の値を残す</summary>
        [Tooltip("Leave the value as it was while face tracking was active")]
        Keep,

        /// <summary>フェイストラッキングを有効にする直前の値へ戻す</summary>
        [Tooltip("Write back the value the parameter had before face tracking was turned on")]
        Restore
    }

    /// <summary>
    /// 追加パラメータの同期の扱い
    ///
    /// 同期パラメータは装着者のクライアントだけで書き、値は同期でほかの人へ届ける。
    /// 同期しないパラメータは、各クライアントがそれぞれ書く。
    /// 宣言順はシリアライズされた値の意味そのものなので入れ替えない
    /// </summary>
    public enum ExtraSyncMode
    {
        /// <summary>Expression Parameters、MA Parameters、MA Menu Itemの設定から判定する</summary>
        [Tooltip("Read it from Expression Parameters, MA Parameters and MA Menu Item")]
        Auto,

        [Tooltip("The parameter is synced. Only the wearer's client writes it")]
        Synced,

        [Tooltip("The parameter is not synced. Every client writes it")]
        Unsynced
    }
}
