using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using FEJsTBridge.Handler;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// アバタールートに付いたコンポーネントを専用のオブジェクトへ移すメニューを検証する
    /// </summary>
    public class SeparateBridgeMenuTests
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

        private GameObject CreateAvatar()
        {
            var root = new GameObject("Avatar");
            _created.Add(root);
            root.AddComponent<VRCAvatarDescriptor>();
            return root;
        }

        [Test]
        public void Separate_MovesComponentToChild_KeepingSettings()
        {
            var avatar = CreateAvatar();
            var source = avatar.AddComponent<FEJsTBridgeComponent>();
            source.faceEmoteIndex = 3;
            source.bypassTrigger = BypassTrigger.LipTrackingOnly;
            source.enableTrackingReapply = false;
            source.reapplyDelaySeconds = 0.5f;
            source.removeFxLayers = new List<string> { "Blink" };
            source.extraParameters = new List<ExtraParameterEntry>
            {
                new ExtraParameterEntry { source = ExtraParameterSource.ParameterName, parameterName = "Glasses" },
            };

            var separated = SeparateBridgeMenu.Separate(source);

            Assert.That(avatar.GetComponent<FEJsTBridgeComponent>(), Is.Null);
            Assert.That(separated.name, Is.EqualTo(CreateBridgeMenu.ObjectName));
            Assert.That(separated.transform.parent, Is.SameAs(avatar.transform));

            var moved = separated.GetComponent<FEJsTBridgeComponent>();
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.faceEmoteIndex, Is.EqualTo(3));
            Assert.That(moved.bypassTrigger, Is.EqualTo(BypassTrigger.LipTrackingOnly));
            Assert.That(moved.enableTrackingReapply, Is.False);
            Assert.That(moved.reapplyDelaySeconds, Is.EqualTo(0.5f));
            Assert.That(moved.removeFxLayers, Is.EqualTo(new[] { "Blink" }));
            Assert.That(moved.extraParameters, Has.Count.EqualTo(1));
            Assert.That(moved.extraParameters[0].source, Is.EqualTo(ExtraParameterSource.ParameterName));
            Assert.That(moved.extraParameters[0].parameterName, Is.EqualTo("Glasses"));
        }

        [Test]
        public void Separate_CanBeUndoneAtOnce()
        {
            var avatar = CreateAvatar();
            var source = avatar.AddComponent<FEJsTBridgeComponent>();
            source.faceEmoteIndex = 3;

            SeparateBridgeMenu.Separate(source);
            Undo.PerformUndo();

            var restored = avatar.GetComponent<FEJsTBridgeComponent>();
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.faceEmoteIndex, Is.EqualTo(3));
            Assert.That(avatar.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void CanSeparate_IsTrueOnlyOnAvatarRoot()
        {
            var avatar = CreateAvatar();
            var onRoot = avatar.AddComponent<FEJsTBridgeComponent>();

            var otherAvatar = CreateAvatar();
            var child = new GameObject("Child");
            child.transform.SetParent(otherAvatar.transform, false);
            var onChild = child.AddComponent<FEJsTBridgeComponent>();

            Assert.That(SeparateBridgeMenu.CanSeparate(onRoot), Is.True);
            Assert.That(SeparateBridgeMenu.CanSeparate(onChild), Is.False);
            Assert.That(SeparateBridgeMenu.CanSeparate(null), Is.False);
        }
    }
}
