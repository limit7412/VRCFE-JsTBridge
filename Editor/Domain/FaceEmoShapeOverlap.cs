using System.Collections.Generic;
using System.Linq;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// FaceEmoとJerryが同じブレンドシェイプを書いているか
    ///
    /// 表情制御方式ではFaceEmoをバイパスさせないため、デフォルト表情のレイヤーが
    /// 顔のブレンドシェイプを既定値で書き続ける。トラッキングが動かすシェイプが
    /// そこに含まれていると、トラッキングの値は毎フレーム打ち消される。
    /// 分けるにはFaceEmoの「除外するブレンドシェイプ」へ登録してもらうしかないため、
    /// 重なりを見つけて名前を示す材料にする。
    /// </summary>
    internal readonly struct FaceEmoShapeOverlap
    {
        /// <summary>警告に出すブレンドシェイプ名の数</summary>
        public const int SampleCount = 5;

        public FaceEmoShapeOverlap(int count, IReadOnlyList<string> sampleShapeNames)
        {
            Count = count;
            SampleShapeNames = sampleShapeNames ?? new string[0];
        }

        /// <summary>重なっているブレンドシェイプの数</summary>
        public int Count { get; }

        /// <summary>表示に出す名前。多いときは先頭のいくつかだけ</summary>
        public IReadOnlyList<string> SampleShapeNames { get; }

        public bool IsEmpty => Count == 0;

        public static FaceEmoShapeOverlap None => new FaceEmoShapeOverlap(0, new string[0]);

        public static FaceEmoShapeOverlap Detect(
            IReadOnlyCollection<string> faceEmoBindings,
            IReadOnlyCollection<string> trackingBindings)
        {
            if (faceEmoBindings == null || trackingBindings == null)
            {
                return None;
            }

            var tracking = new HashSet<string>(trackingBindings);
            var shared = faceEmoBindings.Where(tracking.Contains).ToArray();

            if (shared.Length == 0)
            {
                return None;
            }

            // 束縛の数ではなく名前の数で数える。利用者が除外設定へ入れる単位は名前である
            var names = BlendShapeBinding.ToShapeNames(shared).OrderBy(name => name).ToArray();

            return new FaceEmoShapeOverlap(names.Length, names.Take(SampleCount).ToArray());
        }
    }
}
