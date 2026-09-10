using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

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
        [Tooltip("How to stop FaceEmo from fighting face tracking. Bypass: stop FaceEmo entirely. ExpressionControl: keep FaceEmo running, and lock the expression, stop blinking, and switch to the chosen emote")]
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
    }

    /// <summary>
    /// FaceEmoの書き込みを止める方式
    /// </summary>
    public enum ControlMethod
    {
        /// <summary>
        /// FaceEmoの外部連携用パラメータでバイパスさせ、FaceEmoごと止める
        /// 接点が1本で済み、FaceEmo側の設定にも依存しない
        /// </summary>
        [Tooltip("Stop FaceEmo entirely through its bypass parameter")]
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
}
