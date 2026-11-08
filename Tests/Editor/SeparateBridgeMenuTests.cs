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
        public void MoveToSeparateObject_MovesComponentToNewChild_KeepingSettings()
        {
            var avatar = CreateAvatar();
            var source = avatar.AddComponent<FEJsTBridgeComponent>();
            source.faceEmoteIndex = 3;
            source.bypassTrigger = BypassTrigger.LipTrackingOnly;
            source.enableTrackingReapply = false;
            source.reapplyDelaySeconds = 0.5f;
            source.removeFxLayers.Add("Conflicting Layer");

            var created = SeparateBridgeMenu.MoveToSeparateObject(source);

            Assert.That(avatar.GetComponent<FEJsTBridgeComponent>(), Is.Null);
            Assert.That(created.name, Is.EqualTo(CreateBridgeMenu.ObjectName));
            Assert.That(created.transform.parent, Is.SameAs(avatar.transform));

            var moved = created.GetComponent<FEJsTBridgeComponent>();
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.faceEmoteIndex, Is.EqualTo(3));
            Assert.That(moved.bypassTrigger, Is.EqualTo(BypassTrigger.LipTrackingOnly));
            Assert.That(moved.enableTrackingReapply, Is.False);
            Assert.That(moved.reapplyDelaySeconds, Is.EqualTo(0.5f));
            Assert.That(moved.removeFxLayers, Is.EqualTo(new[] { "Conflicting Layer" }));
        }

        [Test]
        public void MoveToSeparateObject_AlignsNewObjectToAvatarRoot()
        {
            var avatar = CreateAvatar();
            avatar.transform.position = new Vector3(1f, 2f, 3f);
            avatar.layer = 5;
            var source = avatar.AddComponent<FEJsTBridgeComponent>();

            var created = SeparateBridgeMenu.MoveToSeparateObject(source);

            Assert.That(created.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(created.layer, Is.EqualTo(5));
        }

        [Test]
        public void MoveToSeparateObject_IsRevertedBySingleUndo()
        {
            var avatar = CreateAvatar();
            var source = avatar.AddComponent<FEJsTBridgeComponent>();
            source.faceEmoteIndex = 2;

            var created = SeparateBridgeMenu.MoveToSeparateObject(source);
            Undo.PerformUndo();

            Assert.That(created == null, Is.True);
            var restored = avatar.GetComponent<FEJsTBridgeComponent>();
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.faceEmoteIndex, Is.EqualTo(2));
        }

        [Test]
        public void FindMovableComponent_ReturnsComponentOnlyOnAvatarRoot()
        {
            var avatar = CreateAvatar();
            var child = new GameObject("Child");
            child.transform.SetParent(avatar.transform, false);
            child.AddComponent<FEJsTBridgeComponent>();

            // アバタールート以外に置いたものは、利用者が意図してそこへ置いたものとして対象外にする
            Assert.That(SeparateBridgeMenu.FindMovableComponent(child), Is.Null);
            Assert.That(SeparateBridgeMenu.FindMovableComponent(avatar), Is.Null);
            Assert.That(SeparateBridgeMenu.FindMovableComponent(null), Is.Null);

            Object.DestroyImmediate(child.GetComponent<FEJsTBridgeComponent>());
            var onRoot = avatar.AddComponent<FEJsTBridgeComponent>();
            Assert.That(SeparateBridgeMenu.FindMovableComponent(avatar), Is.SameAs(onRoot));
        }
    }
}
