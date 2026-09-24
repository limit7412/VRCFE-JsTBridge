using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// 追加パラメータの値の組み立てと、MAのトグル値の規則のなぞりを検証する
    /// </summary>
    public class ExtraParameterTargetTests
    {
        [Test]
        public void FromDirect_UsesReleasedValue_WhenRevert()
        {
            var target = ExtraParameterTarget.FromDirect(
                "Mask", ExtraParameterType.Int, 3f, ExtraReleaseMode.Revert, 1f);

            Assert.That(target.Name, Is.EqualTo("Mask"));
            Assert.That(target.Type, Is.EqualTo(BridgeParameterType.Int));
            Assert.That(target.EngagedValue, Is.EqualTo(3f));
            Assert.That(target.ReleasedValue, Is.EqualTo(1f));
        }

        [Test]
        public void FromDirect_HasNoReleasedValue_WhenKeep()
        {
            var target = ExtraParameterTarget.FromDirect(
                "Mask", ExtraParameterType.Int, 3f, ExtraReleaseMode.Keep, 1f);

            Assert.That(target.ReleasedValue, Is.Null);
        }

        [Test]
        public void FromDirect_NormalizesValuesToType()
        {
            var boolean = ExtraParameterTarget.FromDirect(
                "Blush", ExtraParameterType.Bool, 0.3f, ExtraReleaseMode.Revert, 0f);
            Assert.That(boolean.EngagedValue, Is.EqualTo(1f));
            Assert.That(boolean.ReleasedValue, Is.EqualTo(0f));

            var integer = ExtraParameterTarget.FromDirect(
                "Mask", ExtraParameterType.Int, 2.4f, ExtraReleaseMode.Revert, 0.6f);
            Assert.That(integer.EngagedValue, Is.EqualTo(2f));
            Assert.That(integer.ReleasedValue, Is.EqualTo(1f));

            var real = ExtraParameterTarget.FromDirect(
                "Hue", ExtraParameterType.Float, 0.25f, ExtraReleaseMode.Revert, -0.5f);
            Assert.That(real.EngagedValue, Is.EqualTo(0.25f));
            Assert.That(real.ReleasedValue, Is.EqualTo(-0.5f));
        }

        [Test]
        public void FromMenuItem_WritesToggleValue_WhenOn()
        {
            var target = ExtraParameterTarget.FromMenuItem(
                "Outfit", BridgeParameterType.Int, 3f, ExtraToggleState.On, ExtraReleaseMode.Revert);

            Assert.That(target.EngagedValue, Is.EqualTo(3f));
            Assert.That(target.ReleasedValue, Is.EqualTo(0f));
        }

        [Test]
        public void FromMenuItem_WritesZero_WhenOff()
        {
            var target = ExtraParameterTarget.FromMenuItem(
                "Blush", BridgeParameterType.Bool, 1f, ExtraToggleState.Off, ExtraReleaseMode.Revert);

            Assert.That(target.EngagedValue, Is.EqualTo(0f));
            Assert.That(target.ReleasedValue, Is.EqualTo(1f));
        }

        [Test]
        public void FromMenuItem_HasNoReleasedValue_WhenKeep()
        {
            var target = ExtraParameterTarget.FromMenuItem(
                "Blush", BridgeParameterType.Bool, 1f, ExtraToggleState.Off, ExtraReleaseMode.Keep);

            Assert.That(target.ReleasedValue, Is.Null);
        }

        // 期待値を型名で渡すのは、BridgeParameterTypeがinternalで公開メソッドの引数に書けないため
        [TestCase(0f, "Bool")]
        [TestCase(1f, "Bool")]
        [TestCase(2f, "Int")]
        [TestCase(255f, "Int")]
        [TestCase(0.5f, "Float")]
        [TestCase(-1f, "Float")]
        public void TypeOf_FollowsModularAvatarRule(float value, string expected)
        {
            Assert.That(MenuItemToggleValue.TypeOf(value).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void ResolveAutomatic_ReturnsOne_WhenUndeclared()
        {
            Assert.That(MenuItemToggleValue.ResolveAutomatic(false, 1, null), Is.EqualTo(1f));
            Assert.That(MenuItemToggleValue.ResolveAutomatic(true, 1, null), Is.EqualTo(1f));
        }

        [Test]
        public void ResolveAutomatic_UsesDeclaredDefault_WhenDefault()
        {
            Assert.That(MenuItemToggleValue.ResolveAutomatic(true, 1, 0f), Is.EqualTo(0f));
            Assert.That(MenuItemToggleValue.ResolveAutomatic(true, 1, 3f), Is.EqualTo(3f));
        }

        [Test]
        public void ResolveAutomatic_AvoidsDeclaredDefault_WhenNotDefault()
        {
            Assert.That(MenuItemToggleValue.ResolveAutomatic(false, 1, 0f), Is.EqualTo(1f));
            Assert.That(MenuItemToggleValue.ResolveAutomatic(false, 1, 1f), Is.EqualTo(2f));
        }

        [Test]
        public void ResolveAutomatic_ReturnsNull_WhenMenuItemsShareParameter()
        {
            Assert.That(MenuItemToggleValue.ResolveAutomatic(false, 2, null), Is.Null);
        }
    }
}
