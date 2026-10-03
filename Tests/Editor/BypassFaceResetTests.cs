using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// バイパス中にFaceEmoの表情が顔へ残るかの判定を検証する
    /// </summary>
    public class BypassFaceResetTests
    {
        private static FaceEmoWriteDefaults FaceEmo(bool matchAvatar, bool hasOffState)
        {
            return new FaceEmoWriteDefaults(matchAvatar, hasOffState);
        }

        [Test]
        public void LeavesFace_IsTrue_WhenFaceEmoIsMatchedToAvatarWithWriteDefaultsOff()
        {
            // FaceEmo自身はWrite Defaults ONで生成されていても、マージ時にOFFへ書き換わる
            Assert.That(
                BypassFaceReset.LeavesFace(false, new[] { FaceEmo(matchAvatar: true, hasOffState: false) }),
                Is.True);
        }

        [Test]
        public void LeavesFace_IsFalse_WhenFaceEmoIsMatchedToAvatarWithWriteDefaultsOn()
        {
            Assert.That(
                BypassFaceReset.LeavesFace(true, new[] { FaceEmo(matchAvatar: true, hasOffState: true) }),
                Is.False);
        }

        /// <summary>
        /// アバター側が揃っていなければ、Modular Avatarは書き換えない
        /// </summary>
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void LeavesFace_FollowsController_WhenAvatarWriteDefaultsIsMixed(bool hasOffState, bool expected)
        {
            Assert.That(
                BypassFaceReset.LeavesFace(null, new[] { FaceEmo(matchAvatar: true, hasOffState) }),
                Is.EqualTo(expected));
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void LeavesFace_FollowsController_WhenFaceEmoIsNotMatchedToAvatar(bool hasOffState, bool expected)
        {
            Assert.That(
                BypassFaceReset.LeavesFace(false, new[] { FaceEmo(matchAvatar: false, hasOffState) }),
                Is.EqualTo(expected));
        }

        [Test]
        public void LeavesFace_IsTrue_WhenAnyControllerRunsWithWriteDefaultsOff()
        {
            var controllers = new[]
            {
                FaceEmo(matchAvatar: false, hasOffState: false),
                FaceEmo(matchAvatar: false, hasOffState: true),
            };

            Assert.That(BypassFaceReset.LeavesFace(true, controllers), Is.True);
        }

        [Test]
        public void LeavesFace_IsFalse_WhenFaceEmoIsAbsent()
        {
            Assert.That(BypassFaceReset.LeavesFace(false, new FaceEmoWriteDefaults[0]), Is.False);
            Assert.That(BypassFaceReset.LeavesFace(false, null), Is.False);
        }
    }
}
