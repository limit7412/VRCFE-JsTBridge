using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using FEJsTBridge.Domain;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// Merge Animatorの走査による環境検出を検証する
    /// </summary>
    public class AvatarEnvironmentScannerTests
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

        private void AddMergeAnimator(GameObject avatarRoot, string childName, bool active, params string[] parameters)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(avatarRoot.transform, false);
            child.SetActive(active);

            var controller = new AnimatorController { name = childName + " Controller" };
            _created.Add(controller);

            foreach (var parameter in parameters)
            {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
            }

            var mergeAnimator = child.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
        }

        [Test]
        public void Scan_DetectsBothPackages_FromMergeAnimators()
        {
            var root = CreateAvatarRoot();
            AddMergeAnimator(root, "Jerry", true,
                BridgeParameterNames.FacialExpressionsDisabled,
                BridgeParameterNames.EyeTrackingActive);
            AddMergeAnimator(root, "FaceEmo", true, BridgeParameterNames.ForceBypassEnable);

            var report = AvatarEnvironmentScanner.Scan(root);

            Assert.That(report.JerryDetected, Is.True);
            Assert.That(report.FaceEmoDetected, Is.True);
        }

        [Test]
        public void Scan_IncludesInactiveObjects()
        {
            var root = CreateAvatarRoot();
            AddMergeAnimator(root, "Jerry", false,
                BridgeParameterNames.FacialExpressionsDisabled,
                BridgeParameterNames.EyeTrackingActive);

            var report = AvatarEnvironmentScanner.Scan(root);

            Assert.That(report.JerryDetected, Is.True);
            Assert.That(report.FaceEmoDetected, Is.False);
        }

        [Test]
        public void Scan_DetectsNothing_WhenNoMergeAnimatorExists()
        {
            var report = AvatarEnvironmentScanner.Scan(CreateAvatarRoot());

            Assert.That(report.JerryDetected, Is.False);
            Assert.That(report.FaceEmoDetected, Is.False);
        }

        [Test]
        public void Scan_IgnoresMergeAnimatorWithoutController()
        {
            var root = CreateAvatarRoot();
            var child = new GameObject("Empty");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<ModularAvatarMergeAnimator>();

            var report = AvatarEnvironmentScanner.Scan(root);

            Assert.That(report.JerryDetected, Is.False);
            Assert.That(report.FaceEmoDetected, Is.False);
        }
    }
}
