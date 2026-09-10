using System.Collections.Generic;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 表情制御方式が書き込むFaceEmo側のパラメータ名
    ///
    /// FaceEmoはこれらをMA Parametersへ登録しており、AddParameterPrefix設定が有効なアバターでは
    /// FaceEmo_ を前置した名前へリネームされる。リネームはMA Parametersに登録された名前にしか
    /// 働かないため、ブリッジのコントローラにはリネーム後の名前を宣言しなければ接続されない。
    ///
    /// バイパス方式が使うCN_FORCE_BYPASS_ENABLEだけはMA Parametersに登録されないため、
    /// この解決の対象にならない。
    /// </summary>
    internal readonly struct FaceEmoParameterNames
    {
        public FaceEmoParameterNames(string emoteLockEnable, string forceBlinkDisable, string emote, bool resolved)
        {
            EmoteLockEnable = emoteLockEnable;
            ForceBlinkDisable = forceBlinkDisable;
            Emote = emote;
            Resolved = resolved;
        }

        public string EmoteLockEnable { get; }

        public string ForceBlinkDisable { get; }

        public string Emote { get; }

        /// <summary>
        /// FaceEmoのMA Parametersから名前を引けたか
        ///
        /// 引けなかった場合はFaceEmoの生の名前を持つ。
        /// プレフィックスを使っていないアバターならそれで正しく接続されるが、
        /// 使っているアバターでは何も起きないため、ビルド時に警告する材料にする。
        /// </summary>
        public bool Resolved { get; }

        /// <summary>リネームを解決できなかったときに使うFaceEmoの生の名前</summary>
        public static FaceEmoParameterNames Raw =>
            new FaceEmoParameterNames(
                BridgeParameterNames.EmoteLockEnable,
                BridgeParameterNames.ForceBlinkDisable,
                BridgeParameterNames.Emote,
                resolved: false);

        /// <summary>
        /// MA Parametersの登録内容から名前を解決する
        /// </summary>
        /// <param name="parameterRemaps">
        /// MA Parametersコンポーネント1つにつき1件の、元の名前からリネーム後の名前への対応。
        /// リネームしない登録は、元の名前をそのまま値に持つ
        /// </param>
        /// <remarks>
        /// 3つすべてを登録しているコンポーネントだけをFaceEmoのものとみなす。
        /// FaceEmoは制御パラメータを1つのMA Parametersへまとめて登録するため、
        /// 一部だけを持つコンポーネントは別のツールのものである。
        /// </remarks>
        public static FaceEmoParameterNames Resolve(
            IEnumerable<IReadOnlyDictionary<string, string>> parameterRemaps)
        {
            if (parameterRemaps == null)
            {
                return Raw;
            }

            foreach (var remap in parameterRemaps)
            {
                if (remap == null)
                {
                    continue;
                }

                if (!remap.TryGetValue(BridgeParameterNames.EmoteLockEnable, out var emoteLockEnable)
                    || !remap.TryGetValue(BridgeParameterNames.ForceBlinkDisable, out var forceBlinkDisable)
                    || !remap.TryGetValue(BridgeParameterNames.Emote, out var emote))
                {
                    continue;
                }

                return new FaceEmoParameterNames(emoteLockEnable, forceBlinkDisable, emote, resolved: true);
            }

            return Raw;
        }
    }
}
