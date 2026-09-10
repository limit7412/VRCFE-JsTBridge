using System.Collections.Generic;
using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// FaceEmoのパラメータ名の解決を検証する
    /// </summary>
    public class FaceEmoParameterNamesTests
    {
        private const string Prefix = "FaceEmo_";

        private static IReadOnlyDictionary<string, string> FaceEmoRemap(string prefix)
        {
            return new Dictionary<string, string>
            {
                { BridgeParameterNames.EmoteLockEnable, prefix + BridgeParameterNames.EmoteLockEnable },
                { BridgeParameterNames.ForceBlinkDisable, prefix + BridgeParameterNames.ForceBlinkDisable },
                { BridgeParameterNames.Emote, prefix + BridgeParameterNames.Emote },
                { "CN_BYPASS", prefix + "CN_BYPASS" },
            };
        }

        [Test]
        public void Resolve_ReadsRenamedNames()
        {
            var names = FaceEmoParameterNames.Resolve(new[] { FaceEmoRemap(Prefix) });

            Assert.That(names.Resolved, Is.True);
            Assert.That(names.EmoteLockEnable, Is.EqualTo(Prefix + BridgeParameterNames.EmoteLockEnable));
            Assert.That(names.ForceBlinkDisable, Is.EqualTo(Prefix + BridgeParameterNames.ForceBlinkDisable));
            Assert.That(names.Emote, Is.EqualTo(Prefix + BridgeParameterNames.Emote));
        }

        [Test]
        public void Resolve_KeepsNames_WhenNotRenamed()
        {
            var names = FaceEmoParameterNames.Resolve(new[] { FaceEmoRemap(string.Empty) });

            Assert.That(names.Resolved, Is.True);
            Assert.That(names.EmoteLockEnable, Is.EqualTo(BridgeParameterNames.EmoteLockEnable));
            Assert.That(names.Emote, Is.EqualTo(BridgeParameterNames.Emote));
        }

        /// <summary>
        /// FaceEmoは制御パラメータを1つのMA Parametersへまとめて登録する。
        /// 一部だけを持つコンポーネントは別のツールのものであり、そこから名前を引いてはいけない
        /// </summary>
        [Test]
        public void Resolve_IgnoresComponentsWithOnlySomeOfTheNames()
        {
            var partial = new Dictionary<string, string>
            {
                { BridgeParameterNames.Emote, "Other_" + BridgeParameterNames.Emote },
            };

            var names = FaceEmoParameterNames.Resolve(
                new IReadOnlyDictionary<string, string>[] { partial, FaceEmoRemap(Prefix) });

            Assert.That(names.Emote, Is.EqualTo(Prefix + BridgeParameterNames.Emote));
        }

        [Test]
        public void Resolve_FallsBackToRawNames_WhenNothingMatches()
        {
            var unrelated = new Dictionary<string, string> { { "Unrelated", "Unrelated" } };

            var names = FaceEmoParameterNames.Resolve(
                new IReadOnlyDictionary<string, string>[] { unrelated });

            Assert.That(names.Resolved, Is.False);
            Assert.That(names.EmoteLockEnable, Is.EqualTo(BridgeParameterNames.EmoteLockEnable));
            Assert.That(names.ForceBlinkDisable, Is.EqualTo(BridgeParameterNames.ForceBlinkDisable));
            Assert.That(names.Emote, Is.EqualTo(BridgeParameterNames.Emote));
        }

        [Test]
        public void Resolve_FallsBackToRawNames_WhenNull()
        {
            var names = FaceEmoParameterNames.Resolve(null);

            Assert.That(names.Resolved, Is.False);
            Assert.That(names.Emote, Is.EqualTo(BridgeParameterNames.Emote));
        }
    }
}
