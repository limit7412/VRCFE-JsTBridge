using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using FEJsTBridge.Domain;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// パラメータ名が空のメニューアイテムを、MAが振る名前へ接続できるかを検証する
    /// </summary>
    public class ExtraParameterResolverTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            _created.Clear();
        }

        private GameObject CreateAvatarRoot()
        {
            var root = new GameObject("Avatar");
            _created.Add(root);
            return root;
        }

        private static ModularAvatarMenuItem AddToggle(GameObject parent, string objectName, bool withReactive)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent.transform);

            var item = child.AddComponent<ModularAvatarMenuItem>();
            item.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                value = 1f,
            };
            item.automaticValue = true;
            item.isSynced = true;

            if (withReactive)
            {
                child.AddComponent<ModularAvatarObjectToggle>();
            }

            return item;
        }

        private static ExtraParameterEntry EntryFor(ModularAvatarMenuItem item)
        {
            return new ExtraParameterEntry
            {
                source = ExtraParameterSource.MenuItem,
                menuItem = item,
                menuItemState = ExtraToggleState.On,
                releaseMode = ExtraReleaseMode.Restore,
                syncMode = ExtraSyncMode.Auto,
            };
        }

        [Test]
        public void Resolve_BuildsAutomaticName_WhenMenuItemHasReactiveComponent()
        {
            var root = CreateAvatarRoot();
            var menu = new GameObject("Menu");
            menu.transform.SetParent(root.transform);
            var item = AddToggle(menu, "mouse for ft ON", true);

            var result = ExtraParameterResolver.Resolve(
                new BuildContext(root, null), root, new[] { EntryFor(item) });

            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.Targets.Count, Is.EqualTo(1));

            var target = result.Targets[0];
            Assert.That(target.Name, Is.EqualTo("__MA/AutoParam/mouse for ft ON$73684bb48174"));
            Assert.That(target.Type, Is.EqualTo(BridgeParameterType.Bool));
            Assert.That(target.EngagedValue, Is.EqualTo(1f));
            // MAはメニューアイテムのisSyncedで同期パラメータを作る
            Assert.That(target.Synced, Is.True);
        }

        [Test]
        public void Resolve_SkipsWithWarning_WhenMenuItemHasNoReactiveComponent()
        {
            var root = CreateAvatarRoot();
            var item = AddToggle(root, "Plain", false);

            var result = ExtraParameterResolver.Resolve(
                new BuildContext(root, null), root, new[] { EntryFor(item) });

            Assert.That(result.Targets, Is.Empty);
            Assert.That(
                result.Issues.Select(issue => issue.MessageKey),
                Is.EqualTo(new[] { "warning.extra_parameter.unnamed_parameter" }));
        }

        [Test]
        public void Resolve_SkipsWithWarning_WhenSeveralUnnamedItemsShareObject()
        {
            var root = CreateAvatarRoot();
            var item = AddToggle(root, "Shared", true);
            var sibling = item.gameObject.AddComponent<ModularAvatarMenuItem>();
            sibling.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
            };

            var result = ExtraParameterResolver.Resolve(
                new BuildContext(root, null), root, new[] { EntryFor(item) });

            Assert.That(result.Targets, Is.Empty);
            Assert.That(
                result.Issues.Select(issue => issue.MessageKey),
                Is.EqualTo(new[] { "warning.extra_parameter.shared_auto_name" }));
        }
    }
}
