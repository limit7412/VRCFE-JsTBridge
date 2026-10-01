using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// Modular Avatar 1.12以降がパラメータ名の空いたメニューアイテムに振る名前のなぞりを検証する
    /// 期待値はMAのParameterRenameMappings.Remapと同じ手順 (パスのSHA-256の先頭6バイト) で求めた
    /// </summary>
    public class MenuItemAutoParameterNameTests
    {
        [Test]
        public void Build_AppendsPathHashToPrefixedObjectName()
        {
            var name = MenuItemAutoParameterName.Build("mouse for ft ON", "Menu/mouse for ft ON");

            Assert.That(name, Is.EqualTo("__MA/AutoParam/mouse for ft ON$73684bb48174"));
        }

        [Test]
        public void Build_HashesEmptyPath_ForAvatarRoot()
        {
            var name = MenuItemAutoParameterName.Build("Root", "");

            Assert.That(name, Is.EqualTo("__MA/AutoParam/Root$e3b0c44298fc"));
        }

        [Test]
        public void Build_TreatsNullPathAsEmpty()
        {
            Assert.That(
                MenuItemAutoParameterName.Build("Root", null),
                Is.EqualTo(MenuItemAutoParameterName.Build("Root", "")));
        }

        [Test]
        public void Build_DistinguishesSameNameAtDifferentPaths()
        {
            var first = MenuItemAutoParameterName.Build("Toggle", "A/Toggle");
            var second = MenuItemAutoParameterName.Build("Toggle", "B/Toggle");

            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
