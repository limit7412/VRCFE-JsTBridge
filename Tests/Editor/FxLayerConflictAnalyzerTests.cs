using System.Linq;
using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    public class FxLayerConflictAnalyzerTests
    {
        private const string FaceShape = "Body/blendShape.Smile";
        private const string TrackingShape = "Body/blendShape.JawOpen";
        private const string ClothShape = "Skirt/blendShape.Shrink";

        private static FxLayerSnapshot Layer(
            string name, int index, bool changesTrackingControl = false, params string[] bindings)
        {
            return new FxLayerSnapshot(name, index, bindings, changesTrackingControl);
        }

        private static FxLayerSnapshot WriteDefaultsLayer(string name, int index, params string[] bindings)
        {
            return new FxLayerSnapshot(name, index, bindings, false, true);
        }

        [Test]
        public void Analyze_MarksLayerAsCandidate_WhenItWritesTheSameShapes()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { Layer("Left Hand Face", 1, false, FaceShape) },
                new[] { FaceShape, TrackingShape });

            var layer = report.Layers.Single();
            Assert.That(layer.Verdict, Is.EqualTo(FxLayerVerdict.Candidate));
            Assert.That(layer.SharedCount, Is.EqualTo(1));
            Assert.That(layer.SharedShapeNames, Is.EqualTo(new[] { "Smile" }));
        }

        [Test]
        public void Analyze_LeavesLayerAlone_WhenItWritesUnrelatedShapes()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { Layer("Skirt Toggle", 1, false, ClothShape) },
                new[] { FaceShape });

            // 服のトグルもブレンドシェイプを書くが、表情とは重ならない
            Assert.That(report.Layers.Single().Verdict, Is.EqualTo(FxLayerVerdict.NoConflict));
            Assert.That(report.Candidates, Is.Empty);
        }

        [Test]
        public void Analyze_MarksLayerAsCandidate_WhenItChangesTrackingControl()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { Layer("Right Hand Tracking Control", 2, true) },
                new[] { FaceShape });

            var layer = report.Layers.Single();
            Assert.That(layer.Verdict, Is.EqualTo(FxLayerVerdict.Candidate));
            Assert.That(layer.ChangesTrackingControl, Is.True);
            Assert.That(layer.SharedCount, Is.EqualTo(0));
        }

        [Test]
        public void Analyze_TreatsEveryBlendShapeLayerAsCandidate_WhenNothingToCompareAgainst()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { Layer("Skirt Toggle", 1, false, ClothShape) },
                new string[0]);

            Assert.That(report.HasReference, Is.False);
            Assert.That(report.Layers.Single().Verdict, Is.EqualTo(FxLayerVerdict.Candidate));
        }

        /// <summary>
        /// 素体のFXにFaceEmoの生成物が焼き込まれている場合、名前では除外しない
        /// 焼き込まれたFaceEmoはバイパスで止まらないため、書く内容で判定する
        /// </summary>
        [Test]
        public void Analyze_JudgesBakedFaceEmoLayersByContent()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[]
                {
                    Layer("[ USER EDIT ] DEFAULT FACE", 0, false, FaceShape),
                    Layer("[ USER EDIT ] FACE EMOTE PLAYER", 1, false, FaceShape),
                    Layer("FACE EMOTE CONTROL", 2),
                    Layer("MA Responsive: BodyAll", 3, false, ClothShape),
                },
                new[] { FaceShape });

            Assert.That(
                report.Candidates.Select(layer => layer.LayerName),
                Is.EquivalentTo(new[] { "[ USER EDIT ] DEFAULT FACE", "[ USER EDIT ] FACE EMOTE PLAYER" }));
            Assert.That(report.Layers[2].Verdict, Is.EqualTo(FxLayerVerdict.NoConflict));
            Assert.That(report.Layers[3].Verdict, Is.EqualTo(FxLayerVerdict.NoConflict));
        }

        [Test]
        public void Analyze_TreatsGenericNameAsCandidate_WhenItWritesSharedShapes()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { Layer("BLINK", 1, false, FaceShape) },
                new[] { FaceShape });

            Assert.That(report.Candidates.Single().LayerName, Is.EqualTo("BLINK"));
        }

        [Test]
        public void Analyze_DoesNotGuess_WhenReferenceIsEmptyButPackageWasFound()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[]
                {
                    Layer("Skirt Toggle", 1, false, ClothShape),
                    Layer("Right Hand Tracking Control", 2, true),
                },
                new string[0],
                false);

            // ブレンドシェイプでの判定はしないが、Tracking Controlは根拠になる
            Assert.That(report.Layers[0].Verdict, Is.EqualTo(FxLayerVerdict.NoConflict));
            Assert.That(report.Layers[1].Verdict, Is.EqualTo(FxLayerVerdict.Candidate));
        }

        [Test]
        public void HasUnjudgedWriteDefaults_IsTrue_WhenAnUnlistedLayerHasThem()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { WriteDefaultsLayer("Skirt Toggle", 1, ClothShape) },
                new[] { FaceShape });

            Assert.That(report.Candidates, Is.Empty);
            Assert.That(report.HasUnjudgedWriteDefaults, Is.True);
        }

        [Test]
        public void HasUnjudgedWriteDefaults_IsFalse_WhenTheLayerIsAlreadyACandidate()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { WriteDefaultsLayer("Left Hand Face", 1, FaceShape) },
                new[] { FaceShape });

            Assert.That(report.Candidates.Count(), Is.EqualTo(1));
            Assert.That(report.HasUnjudgedWriteDefaults, Is.False);
        }

        [Test]
        public void Analyze_KeepsLayerOrderAndIndices()
        {
            var report = FxLayerConflictAnalyzer.Analyze(
                new[]
                {
                    Layer("Base Layer", 0),
                    Layer("Left Hand Face", 1, false, FaceShape),
                },
                new[] { FaceShape });

            Assert.That(report.Layers.Select(layer => layer.LayerIndex), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(report.Candidates.Single().LayerName, Is.EqualTo("Left Hand Face"));
        }

        [Test]
        public void Analyze_LimitsTheShapeNamesItShows()
        {
            var shapes = Enumerable.Range(0, 12).Select(i => $"Body/blendShape.Shape{i:00}").ToArray();

            var report = FxLayerConflictAnalyzer.Analyze(
                new[] { Layer("Left Hand Face", 1, false, shapes) }, shapes);

            var layer = report.Layers.Single();
            Assert.That(layer.SharedCount, Is.EqualTo(12));
            Assert.That(layer.SharedShapeNames.Count, Is.EqualTo(5));
        }
    }
}
