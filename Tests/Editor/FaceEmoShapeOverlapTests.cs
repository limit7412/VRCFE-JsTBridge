using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// FaceEmoとトラッキングが書くブレンドシェイプの重なりの検出を検証する
    /// </summary>
    public class FaceEmoShapeOverlapTests
    {
        private static string Binding(string path, string shape)
        {
            return $"{path}/{BlendShapeBinding.PropertyPrefix}{shape}";
        }

        [Test]
        public void Detect_ReportsSharedShapeNames()
        {
            var overlap = FaceEmoShapeOverlap.Detect(
                new[] { Binding("Body", "JawOpen"), Binding("Body", "Smile") },
                new[] { Binding("Body", "JawOpen"), Binding("Body", "EyeBlinkLeft") });

            Assert.That(overlap.IsEmpty, Is.False);
            Assert.That(overlap.Count, Is.EqualTo(1));
            Assert.That(overlap.SampleShapeNames, Is.EqualTo(new[] { "JawOpen" }));
        }

        [Test]
        public void Detect_ReturnsNone_WhenNothingIsShared()
        {
            var overlap = FaceEmoShapeOverlap.Detect(
                new[] { Binding("Body", "Smile") },
                new[] { Binding("Body", "JawOpen") });

            Assert.That(overlap.IsEmpty, Is.True);
            Assert.That(overlap.SampleShapeNames, Is.Empty);
        }

        /// <summary>
        /// 除外設定へ入れる単位は名前なので、メッシュ違いの同名は1つとして数える
        /// </summary>
        [Test]
        public void Detect_CountsNamesInsteadOfBindings()
        {
            var overlap = FaceEmoShapeOverlap.Detect(
                new[] { Binding("Body", "JawOpen"), Binding("Head", "JawOpen") },
                new[] { Binding("Body", "JawOpen"), Binding("Head", "JawOpen") });

            Assert.That(overlap.Count, Is.EqualTo(1));
        }

        [Test]
        public void Detect_LimitsSampleNames()
        {
            var bindings = new string[FaceEmoShapeOverlap.SampleCount + 3];
            for (var i = 0; i < bindings.Length; i++)
            {
                bindings[i] = Binding("Body", $"Shape{i:00}");
            }

            var overlap = FaceEmoShapeOverlap.Detect(bindings, bindings);

            Assert.That(overlap.Count, Is.EqualTo(bindings.Length));
            Assert.That(overlap.SampleShapeNames.Count, Is.EqualTo(FaceEmoShapeOverlap.SampleCount));
            Assert.That(overlap.SampleShapeNames[0], Is.EqualTo("Shape00"));
        }

        [Test]
        public void Detect_ReturnsNone_WhenNull()
        {
            Assert.That(FaceEmoShapeOverlap.Detect(null, new[] { Binding("Body", "JawOpen") }).IsEmpty, Is.True);
            Assert.That(FaceEmoShapeOverlap.Detect(new[] { Binding("Body", "JawOpen") }, null).IsEmpty, Is.True);
        }
    }
}
