using System.Collections.Generic;
using System.Linq;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// FXから取り除くレイヤーの選定結果
    ///
    /// FaceEmoをバイパスすると、FaceEmoが書き込みを止めた分だけ、前にいる素体の表情レイヤーが
    /// 表に出てくる。バイパス中だけの問題に見えるが、素体のレイヤーはFaceEmo使用中は常に
    /// 上書きされていて意味を持たないため、ビルド時にまとめて取り除く。
    /// </summary>
    internal sealed class FxLayerRemovalPlan
    {
        private FxLayerRemovalPlan(IReadOnlyList<int> layerIndices, IReadOnlyList<string> missingNames)
        {
            LayerIndices = layerIndices;
            MissingNames = missingNames;
        }

        /// <summary>取り除くレイヤーの索引。昇順</summary>
        public IReadOnlyList<int> LayerIndices { get; }

        /// <summary>指定されたが見つからなかった名前。打ち間違いの検出に使う</summary>
        public IReadOnlyList<string> MissingNames { get; }

        public bool IsEmpty => LayerIndices.Count == 0;

        /// <summary>
        /// レイヤー名の一覧と、取り除きたい名前の一覧から選定する
        /// 同名のレイヤーが複数あれば、そのすべてを対象にする
        /// </summary>
        public static FxLayerRemovalPlan Resolve(
            IReadOnlyList<string> existingLayerNames,
            IEnumerable<string> requestedNames)
        {
            var indices = new List<int>();
            var missing = new List<string>();

            if (existingLayerNames == null || requestedNames == null)
            {
                return new FxLayerRemovalPlan(indices, missing);
            }

            var seen = new HashSet<string>();

            foreach (var requested in requestedNames)
            {
                // 一覧の空行と、前後の空白は無視する
                var name = requested?.Trim();
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                {
                    continue;
                }

                var matched = false;
                for (var i = 0; i < existingLayerNames.Count; i++)
                {
                    if (existingLayerNames[i] == name)
                    {
                        indices.Add(i);
                        matched = true;
                    }
                }

                if (!matched)
                {
                    missing.Add(name);
                }
            }

            indices.Sort();

            return new FxLayerRemovalPlan(indices, missing);
        }
    }
}
