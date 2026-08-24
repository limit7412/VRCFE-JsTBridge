using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    public class MergeAnimatorInstallerTests
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

        [Test]
        public void Install_AddsMergeAnimator_ToFxLayer()
        {
            var root = new GameObject("Avatar");
            _created.Add(root);

            var controller = new AnimatorController { name = "Bridge" };
            _created.Add(controller);

            var holder = MergeAnimatorInstaller.Install(root, controller);

            Assert.That(holder, Is.Not.Null);
            Assert.That(holder.transform.parent, Is.EqualTo(root.transform));
            Assert.That(holder.name, Is.EqualTo(MergeAnimatorInstaller.HolderObjectName));

            var mergeAnimator = holder.GetComponent<ModularAvatarMergeAnimator>();

            Assert.That(mergeAnimator.animator, Is.SameAs(controller));
            Assert.That(mergeAnimator.layerType, Is.EqualTo(VRCAvatarDescriptor.AnimLayerType.FX));
            Assert.That(mergeAnimator.matchAvatarWriteDefaults, Is.True);
            Assert.That(mergeAnimator.pathMode, Is.EqualTo(MergeAnimatorPathMode.Absolute));
        }
    }
}
