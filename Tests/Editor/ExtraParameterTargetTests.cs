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

        [Test]
        public void FromDirect_Restores_WhenRestore()
        {
            var target = ExtraParameterTarget.FromDirect(
                "Mask", ExtraParameterType.Int, 3f, ExtraReleaseMode.Restore, 1f);

            Assert.That(target.Restore, Is.True);
            Assert.That(target.ReleasedValue, Is.Null);
            Assert.That(target.StashName, Is.EqualTo(ExtraParameterTarget.StashPrefix + "Mask"));
        }

        [Test]
        public void FromMenuItem_Restores_WhenRestore()
        {
            var target = ExtraParameterTarget.FromMenuItem(
                "Blush", BridgeParameterType.Bool, 1f, ExtraToggleState.Off, ExtraReleaseMode.Restore, synced: false);

            Assert.That(target.EngagedValue, Is.EqualTo(0f));
            Assert.That(target.Restore, Is.True);
            Assert.That(target.ReleasedValue, Is.Null);
            Assert.That(target.Synced, Is.False);
        }

        [Test]
        public void IsReservedName_DetectsStashPrefix()
        {
            // 退避先と同じ名前を書き込み先にすると、退避した値が上書きされる
            Assert.That(ExtraParameterTarget.IsReservedName(ExtraParameterTarget.StashPrefix + "Foo"), Is.True);
            Assert.That(ExtraParameterTarget.IsReservedName("Foo"), Is.False);
            Assert.That(ExtraParameterTarget.IsReservedName("fejstbridge/stash/Foo"), Is.False);
            Assert.That(ExtraParameterTarget.IsReservedName(null), Is.False);
        }

        [Test]
        public void SyncResolve_FollowsManualSetting()
        {
            Assert.That(ExtraParameterSync.Resolve(ExtraSyncMode.Synced, false, false), Is.True);
            Assert.That(ExtraParameterSync.Resolve(ExtraSyncMode.Unsynced, true, true), Is.False);
        }

        [Test]
        public void SyncResolve_PrefersDeclaration_OverMenuItem()
        {
            Assert.That(ExtraParameterSync.Resolve(ExtraSyncMode.Auto, false, true), Is.False);
            Assert.That(ExtraParameterSync.Resolve(ExtraSyncMode.Auto, true, false), Is.True);
        }

        [Test]
        public void SyncResolve_UsesMenuItem_WhenUndeclared()
        {
            Assert.That(ExtraParameterSync.Resolve(ExtraSyncMode.Auto, null, true), Is.True);
            Assert.That(ExtraParameterSync.Resolve(ExtraSyncMode.Auto, null, false), Is.False);
        }

        [Test]
        public void SyncResolve_TreatsUnknownAsUnsynced()
        {
            // アニメーターだけのパラメータは同期しない。リモートでも書かないと切り替わらない
            Assert.That(ExtraParameterSync.Resolve(ExtraSyncMode.Auto, null, null), Is.False);
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
