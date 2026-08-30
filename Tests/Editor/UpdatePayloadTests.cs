using NUnit.Framework;
using FEJsTBridge.Infra;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// 更新確認が読み取る2つのJSON (releases/latestの応答とpackage.json) と、
    /// package.jsonの置き場所の解釈の検証
    ///
    /// どのJSONも手元で作ったものではないため、想定と違う形が来ても
    /// 例外を投げずに「分からなかった」として返ることを確かめる。
    /// </summary>
    public class UpdatePayloadTests
    {
        [Test]
        public void TryParseTag_ReadsTheTagFromAReleaseResponse()
        {
            const string json = @"{""tag_name"":""0.1.1"",""name"":""0.1.1"",""prerelease"":false}";

            Assert.That(UpdateCheck.TryParseTag(json, out var tag), Is.True);
            Assert.That(tag, Is.EqualTo("0.1.1"));
        }

        // 応答にはアセット一覧などが並ぶ。必要なキー以外は読み飛ばせること
        [Test]
        public void TryParseTag_IgnoresTheOtherFields()
        {
            const string json =
                @"{""id"":373145658,""tag_name"":""0.2.0"",""assets"":[{""name"":""package.zip""}],""body"":""...""}";

            Assert.That(UpdateCheck.TryParseTag(json, out var tag), Is.True);
            Assert.That(tag, Is.EqualTo("0.2.0"));
        }

        // releases/latestはプレリリースを除いて返すが、除かれなかった場合も通さない
        [Test]
        public void TryParseTag_RejectsAPrereleaseTag()
        {
            const string json = @"{""tag_name"":""0.1.1-test1"",""prerelease"":true}";

            Assert.That(UpdateCheck.TryParseTag(json, out var tag), Is.False);
            Assert.That(tag, Is.Null);
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase(@"{""message"":""Not Found""}")]
        [TestCase(@"{""tag_name"":""""}")]
        public void TryParseTag_Fails_WhenTheResponseIsNotAUsableRelease(string json)
        {
            Assert.That(UpdateCheck.TryParseTag(json, out var tag), Is.False);
            Assert.That(tag, Is.Null);
        }

        [Test]
        public void TryParseVersion_ReadsTheVersionFromAPackageManifest()
        {
            const string json = @"{""name"":""com.qazx7412.kx-vrc-fe-jst-bridge"",""version"":""0.1.0""}";

            Assert.That(PackageLocation.TryParseVersion(json, out var version), Is.True);
            Assert.That(version, Is.EqualTo("0.1.0"));
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase(@"{""name"":""com.example.package""}")]
        [TestCase(@"{""version"":""  ""}")]
        public void TryParseVersion_Fails_WhenTheManifestHasNoVersion(string json)
        {
            Assert.That(PackageLocation.TryParseVersion(json, out var version), Is.False);
            Assert.That(version, Is.Null);
        }

        // Assets/配下・Packages/配下のどちらへ置かれてもpackage.jsonへ辿り着けること
        [TestCase(
            "Packages/com.qazx7412.kx-vrc-fe-jst-bridge/Editor/FEJsTBridge.Editor.asmdef",
            "Packages/com.qazx7412.kx-vrc-fe-jst-bridge")]
        [TestCase(
            "Assets/AtelierKairox/VRCFE-JsTBridge/Editor/FEJsTBridge.Editor.asmdef",
            "Assets/AtelierKairox/VRCFE-JsTBridge")]
        [TestCase(
            @"Packages\com.qazx7412.kx-vrc-fe-jst-bridge\Editor\FEJsTBridge.Editor.asmdef",
            "Packages/com.qazx7412.kx-vrc-fe-jst-bridge")]
        public void TryResolvePackageRoot_TakesTheDirectoryAboveEditor(string asmdefPath, string expected)
        {
            Assert.That(PackageLocation.TryResolvePackageRoot(asmdefPath, out var root), Is.True);
            Assert.That(root, Is.EqualTo(expected));
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("FEJsTBridge.Editor.asmdef")]
        [TestCase("Packages/com.qazx7412.kx-vrc-fe-jst-bridge/FEJsTBridge.Editor.asmdef")]
        [TestCase("Packages/com.qazx7412.kx-vrc-fe-jst-bridge/Runtime/FEJsTBridge.Runtime.asmdef")]
        public void TryResolvePackageRoot_Fails_WhenThePathIsNotUnderEditor(string asmdefPath)
        {
            Assert.That(PackageLocation.TryResolvePackageRoot(asmdefPath, out var root), Is.False);
            Assert.That(root, Is.Null);
        }
    }
}
