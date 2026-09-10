using System;
using System.Collections.Generic;
using System.Linq;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 「パス/blendShape.名前」の形をした束縛の扱い
    /// </summary>
    internal static class BlendShapeBinding
    {
        public const string PropertyPrefix = "blendShape.";

        /// <summary>
        /// 束縛から、表示用にブレンドシェイプ名だけを取り出す
        /// 同じ名前のシェイプが複数のメッシュにあっても、表示としては1つにまとめる
        /// </summary>
        public static IEnumerable<string> ToShapeNames(IEnumerable<string> bindings)
        {
            return bindings
                .Select(binding =>
                {
                    var index = binding.LastIndexOf(PropertyPrefix, StringComparison.Ordinal);
                    return index < 0 ? binding : binding.Substring(index + PropertyPrefix.Length);
                })
                .Distinct();
        }
    }
}
