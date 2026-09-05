using System.Collections.Generic;
using System.Linq;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 解析にかけるレイヤー1つ分の要約
    /// アニメーターのAPIから切り離すことで、判定をUnityなしで検証できる
    /// </summary>
    internal sealed class FxLayerSnapshot
    {
        public FxLayerSnapshot(
            string name,
            int index,
            IReadOnlyCollection<string> blendShapeBindings,
            bool changesTrackingControl,
            bool hasWriteDefaults = false)
        {
            Name = name;
            Index = index;
            BlendShapeBindings = blendShapeBindings ?? new string[0];
            ChangesTrackingControl = changesTrackingControl;
            HasWriteDefaults = hasWriteDefaults;
        }

        public string Name { get; }

        public int Index { get; }

        /// <summary>このレイヤーが書くブレンドシェイプの束縛。「パス/プロパティ名」の形</summary>
        public IReadOnlyCollection<string> BlendShapeBindings { get; }

        /// <summary>Tracking ControlでEyesかMouthを切り替えるか</summary>
        public bool ChangesTrackingControl { get; }

        /// <summary>Write Defaultsが有効なステートを持つか</summary>
        public bool HasWriteDefaults { get; }
    }

    internal enum FxLayerVerdict
    {
        /// <summary>バイパス中に競合する。除去の候補</summary>
        Candidate,

        /// <summary>競合しない</summary>
        NoConflict,
    }

    /// <summary>レイヤー1つ分の判定結果</summary>
    internal sealed class FxLayerConflict
    {
        public FxLayerConflict(
            string layerName,
            int layerIndex,
            FxLayerVerdict verdict,
            IReadOnlyList<string> sharedShapeNames,
            int sharedCount,
            bool changesTrackingControl,
            bool hasWriteDefaults = false)
        {
            LayerName = layerName;
            LayerIndex = layerIndex;
            Verdict = verdict;
            SharedShapeNames = sharedShapeNames;
            SharedCount = sharedCount;
            ChangesTrackingControl = changesTrackingControl;
            HasWriteDefaults = hasWriteDefaults;
        }

        public string LayerName { get; }

        public int LayerIndex { get; }

        public FxLayerVerdict Verdict { get; }

        /// <summary>比較対象と重なったブレンドシェイプの名前。表示用に先頭のいくつかだけ持つ</summary>
        public IReadOnlyList<string> SharedShapeNames { get; }

        /// <summary>重なったブレンドシェイプの総数</summary>
        public int SharedCount { get; }

        public bool ChangesTrackingControl { get; }

        /// <summary>Write Defaultsが有効なステートを持つか</summary>
        public bool HasWriteDefaults { get; }
    }

    /// <summary>解析の結果全体</summary>
    internal sealed class FxLayerConflictReport
    {
        public FxLayerConflictReport(IReadOnlyList<FxLayerConflict> layers, bool hasReference)
        {
            Layers = layers;
            HasReference = hasReference;
        }

        public IReadOnlyList<FxLayerConflict> Layers { get; }

        /// <summary>
        /// 比較対象 (FaceEmoとJerryのブレンドシェイプ) が得られたか
        /// 得られていない場合、判定の根拠は弱くなる
        /// </summary>
        public bool HasReference { get; }

        public IEnumerable<FxLayerConflict> Candidates =>
            Layers.Where(layer => layer.Verdict == FxLayerVerdict.Candidate);

        /// <summary>
        /// 候補に挙がらなかったレイヤーに、Write Defaultsが有効なステートがあるか
        /// </summary>
        /// <remarks>
        /// Write Defaultsが有効なステートは、クリップに書いていないプロパティも既定値へ戻す。
        /// 戻す先には、マージ後に同じAnimatorへ入るFaceEmoやJerryのブレンドシェイプも含まれる。
        /// クリップを読むだけでは分からないため、候補の一覧が網羅でないことを伝えるために使う。
        /// </remarks>
        public bool HasUnjudgedWriteDefaults =>
            Layers.Any(layer => layer.HasWriteDefaults && layer.Verdict == FxLayerVerdict.NoConflict);
    }

    /// <summary>
    /// バイパス中に競合するレイヤーを判定する
    ///
    /// 名前ではなく、レイヤーが実際に何を書くかで判定する。
    /// 服のトグルのようにブレンドシェイプを使うだけのレイヤーを、
    /// 表情レイヤーと取り違えないためである。
    ///
    /// 名前で候補から外すこともしない。
    /// 解析するのは素体のFXであり、Merge Animatorでマージされるレイヤーはそこに現れない。
    /// FaceEmoと同じ名前のレイヤーが素体のFXにあるなら、素体の配布物に焼き込まれたものである。
    /// 焼き込まれたFaceEmoはバイパス用のパラメータを持たず、ブリッジでは止められない。
    /// 表情のブレンドシェイプを書くのなら、他のレイヤーと同じく除去の候補に挙げる。
    /// </summary>
    internal static class FxLayerConflictAnalyzer
    {
        /// <summary>表示に出すブレンドシェイプ名の数</summary>
        private const int SampleCount = 5;

        private const string BlendShapePrefix = "blendShape.";

        /// <summary>
        /// レイヤーごとに競合を判定する
        /// </summary>
        /// <param name="guessWithoutReference">
        /// 比較対象が無いとき、ブレンドシェイプを書くこと自体を根拠にしてよいか。
        /// FaceEmoもJerryも見つからない場合の手がかりとして使う。
        /// どちらかは見つかっているのに束縛が空、という場合は推測してはいけない。
        /// 比較の土台が無いまま、無関係なレイヤーまで候補に挙げることになる。
        /// </param>
        public static FxLayerConflictReport Analyze(
            IReadOnlyList<FxLayerSnapshot> layers,
            IReadOnlyCollection<string> referenceBindings,
            bool guessWithoutReference = true)
        {
            var hasReference = referenceBindings != null && referenceBindings.Count > 0;
            var reference = hasReference
                ? new HashSet<string>(referenceBindings)
                : new HashSet<string>();
            var guesses = !hasReference && guessWithoutReference;

            var results = new List<FxLayerConflict>();

            foreach (var layer in layers ?? new FxLayerSnapshot[0])
            {
                results.Add(Judge(layer, reference, hasReference, guesses));
            }

            return new FxLayerConflictReport(results, hasReference);
        }

        private static FxLayerConflict Judge(
            FxLayerSnapshot layer,
            HashSet<string> reference,
            bool hasReference,
            bool guesses)
        {
            string[] shared;
            if (hasReference)
            {
                shared = layer.BlendShapeBindings.Where(reference.Contains).ToArray();
            }
            else if (guesses)
            {
                // 比較対象が無いときは、ブレンドシェイプを書くこと自体を根拠にする
                shared = layer.BlendShapeBindings.ToArray();
            }
            else
            {
                // 推測してはいけない場合は、Tracking Controlだけで判定する
                shared = new string[0];
            }

            var conflicts = shared.Length > 0 || layer.ChangesTrackingControl;

            return new FxLayerConflict(
                layer.Name,
                layer.Index,
                conflicts ? FxLayerVerdict.Candidate : FxLayerVerdict.NoConflict,
                ToShapeNames(shared).Take(SampleCount).ToArray(),
                shared.Length,
                layer.ChangesTrackingControl,
                layer.HasWriteDefaults);
        }

        /// <summary>
        /// 束縛「パス/blendShape.名前」から、表示用にブレンドシェイプ名だけを取り出す
        /// </summary>
        private static IEnumerable<string> ToShapeNames(IEnumerable<string> bindings)
        {
            return bindings
                .Select(binding =>
                {
                    var index = binding.LastIndexOf(BlendShapePrefix, System.StringComparison.Ordinal);
                    return index < 0 ? binding : binding.Substring(index + BlendShapePrefix.Length);
                })
                .Distinct()
                .OrderBy(name => name);
        }
    }
}
