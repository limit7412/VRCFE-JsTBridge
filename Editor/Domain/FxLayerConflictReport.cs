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
            bool changesTrackingControl)
        {
            Name = name;
            Index = index;
            BlendShapeBindings = blendShapeBindings ?? new string[0];
            ChangesTrackingControl = changesTrackingControl;
        }

        public string Name { get; }

        public int Index { get; }

        /// <summary>このレイヤーが書くブレンドシェイプの束縛。「パス/プロパティ名」の形</summary>
        public IReadOnlyCollection<string> BlendShapeBindings { get; }

        /// <summary>Tracking ControlでEyesかMouthを切り替えるか</summary>
        public bool ChangesTrackingControl { get; }
    }

    internal enum FxLayerVerdict
    {
        /// <summary>バイパス中に競合する。除去の候補</summary>
        Candidate,

        /// <summary>競合しない</summary>
        NoConflict,

        /// <summary>他のツールが管理している。除去してはいけない</summary>
        Managed,
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
            bool changesTrackingControl)
        {
            LayerName = layerName;
            LayerIndex = layerIndex;
            Verdict = verdict;
            SharedShapeNames = sharedShapeNames;
            SharedCount = sharedCount;
            ChangesTrackingControl = changesTrackingControl;
        }

        public string LayerName { get; }

        public int LayerIndex { get; }

        public FxLayerVerdict Verdict { get; }

        /// <summary>比較対象と重なったブレンドシェイプの名前。表示用に先頭のいくつかだけ持つ</summary>
        public IReadOnlyList<string> SharedShapeNames { get; }

        /// <summary>重なったブレンドシェイプの総数</summary>
        public int SharedCount { get; }

        public bool ChangesTrackingControl { get; }
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
    }

    /// <summary>
    /// バイパス中に競合するレイヤーを判定する
    ///
    /// 名前ではなく、レイヤーが実際に何を書くかで判定する。
    /// 服のトグルのようにブレンドシェイプを使うだけのレイヤーを、
    /// 表情レイヤーと取り違えないためである。
    /// </summary>
    internal static class FxLayerConflictAnalyzer
    {
        /// <summary>表示に出すブレンドシェイプ名の数</summary>
        private const int SampleCount = 5;

        private const string BlendShapePrefix = "blendShape.";

        /// <summary>他のツールが管理するレイヤー名の接頭辞</summary>
        private static readonly string[] ManagedNamePrefixes =
        {
            "MA ",
            "Modular Avatar",
            "ModularAvatar",
            "[ USER EDIT ]",
        };

        /// <summary>FaceEmoとブリッジが生成するレイヤー名</summary>
        private static readonly string[] ManagedNames =
        {
            "BLINK",
            "MOUTH MORPH CANCELLER",
            "BYPASS",
            "LOCAL INDICATOR SOUND",
            "DANCE GIMICK CONTROL",
            "INPUT CONVERTER L",
            "INPUT CONVERTER R",
            "FACE EMOTE LOCK",
            "FACE EMOTE CONTROL",
            "FACE EMOTE SET CONTROL",
            BridgePlanBuilder.BypassLayerName,
            BridgePlanBuilder.TrackingReapplyLayerName,
        };

        public static FxLayerConflictReport Analyze(
            IReadOnlyList<FxLayerSnapshot> layers,
            IReadOnlyCollection<string> referenceBindings)
        {
            var hasReference = referenceBindings != null && referenceBindings.Count > 0;
            var reference = hasReference
                ? new HashSet<string>(referenceBindings)
                : new HashSet<string>();

            var results = new List<FxLayerConflict>();

            foreach (var layer in layers ?? new FxLayerSnapshot[0])
            {
                results.Add(Judge(layer, reference, hasReference));
            }

            return new FxLayerConflictReport(results, hasReference);
        }

        private static FxLayerConflict Judge(
            FxLayerSnapshot layer,
            HashSet<string> reference,
            bool hasReference)
        {
            if (IsManagedLayerName(layer.Name))
            {
                return new FxLayerConflict(
                    layer.Name, layer.Index, FxLayerVerdict.Managed, new string[0], 0, layer.ChangesTrackingControl);
            }

            var shared = hasReference
                ? layer.BlendShapeBindings.Where(reference.Contains).ToArray()
                // 比較対象が無いときは、ブレンドシェイプを書くこと自体を根拠にする
                : layer.BlendShapeBindings.ToArray();

            var conflicts = shared.Length > 0 || layer.ChangesTrackingControl;

            return new FxLayerConflict(
                layer.Name,
                layer.Index,
                conflicts ? FxLayerVerdict.Candidate : FxLayerVerdict.NoConflict,
                ToShapeNames(shared).Take(SampleCount).ToArray(),
                shared.Length,
                layer.ChangesTrackingControl);
        }

        /// <summary>
        /// FaceEmoやModular Avatarが生成したレイヤーか
        /// 素体のFXに焼き込まれている場合に、候補として挙げてしまわないようにする
        /// </summary>
        public static bool IsManagedLayerName(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                return false;
            }

            return ManagedNames.Contains(layerName)
                || ManagedNamePrefixes.Any(prefix => layerName.StartsWith(prefix));
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
