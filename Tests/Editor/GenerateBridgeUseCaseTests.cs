using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using FEJsTBridge.Infra;
using FEJsTBridge.UseCase;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    public class GenerateBridgeUseCaseTests
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

        private static FEJsTBridgeComponent AddComponentTo(GameObject parent, string childName = null)
        {
            if (childName == null)
            {
                return parent.AddComponent<FEJsTBridgeComponent>();
            }

            var child = new GameObject(childName);
            child.transform.SetParent(parent.transform, false);
            return child.AddComponent<FEJsTBridgeComponent>();
        }

        [Test]
        public void SelectPrimaryComponent_PrefersComponentOnAvatarRoot()
        {
            var root = CreateAvatarRoot();
            var onChild = AddComponentTo(root, "Child");
            var onRoot = AddComponentTo(root);

            var primary = GenerateBridgeUseCase.SelectPrimaryComponent(
                root, new[] { onChild, onRoot });

            Assert.That(primary, Is.SameAs(onRoot));
        }

        [Test]
        public void SelectPrimaryComponent_ReturnsFirst_WhenNoneOnAvatarRoot()
        {
            var root = CreateAvatarRoot();
            var first = AddComponentTo(root, "First");
            var second = AddComponentTo(root, "Second");

            var primary = GenerateBridgeUseCase.SelectPrimaryComponent(root, new[] { first, second });

            Assert.That(primary, Is.SameAs(first));
        }

        [Test]
        public void SelectPrimaryComponent_ReturnsNull_WhenNoComponentExists()
        {
            Assert.That(
                GenerateBridgeUseCase.SelectPrimaryComponent(CreateAvatarRoot(), new FEJsTBridgeComponent[0]),
                Is.Null);
        }

        [Test]
        public void RemoveComponents_DestroysAllComponents()
        {
            var root = CreateAvatarRoot();
            var components = new[] { AddComponentTo(root), AddComponentTo(root, "Child") };

            GenerateBridgeUseCase.RemoveComponents(components);

            Assert.That(root.GetComponentsInChildren<FEJsTBridgeComponent>(true), Is.Empty);
        }

        [Test]
        public void ExecuteForBuild_InstallsBridge_AndRemovesComponent()
        {
            var root = CreateAvatarRoot();
            AddComponentTo(root);

            GenerateBridgeUseCase.ExecuteForBuild(new BuildContext(root, null));

            Assert.That(root.GetComponentsInChildren<FEJsTBridgeComponent>(true), Is.Empty);

            var mergeAnimator = root.GetComponentInChildren<ModularAvatarMergeAnimator>(true);
            Assert.That(mergeAnimator, Is.Not.Null);
            Assert.That(mergeAnimator.gameObject.name, Is.EqualTo(MergeAnimatorInstaller.HolderObjectName));

            var controller = (AnimatorController)mergeAnimator.animator;
            _created.Add(controller);
            Assert.That(controller.layers.Length, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteForBuild_RemovesDuplicates_AndInstallsOneBridge()
        {
            var root = CreateAvatarRoot();
            AddComponentTo(root);
            AddComponentTo(root, "Child");

            GenerateBridgeUseCase.ExecuteForBuild(new BuildContext(root, null));

            Assert.That(root.GetComponentsInChildren<FEJsTBridgeComponent>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true).Length, Is.EqualTo(1));

            _created.Add(root.GetComponentInChildren<ModularAvatarMergeAnimator>(true).animator);
        }

        [Test]
        public void ExecuteForBuild_DoesNothing_WhenComponentIsAbsent()
        {
            var root = CreateAvatarRoot();

            GenerateBridgeUseCase.ExecuteForBuild(new BuildContext(root, null));

            Assert.That(root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true), Is.Empty);
        }
    }
}
