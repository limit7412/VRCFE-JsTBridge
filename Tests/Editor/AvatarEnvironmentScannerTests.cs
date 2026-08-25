using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
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

        private ModularAvatarMergeAnimator AddMergeAnimator(
            GameObject avatarRoot, string childName, bool active, params string[] parameters)
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
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;

            return mergeAnimator;
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

        [Test]
        public void CollectMergeAnimatorEntries_KeepsBasePathEmpty_ForAbsoluteMode()
        {
            var root = CreateAvatarRoot();
            AddMergeAnimator(root, "FaceEmo", true, BridgeParameterNames.ForceBypassEnable);

            var entry = AvatarEnvironmentScanner.CollectMergeAnimatorEntries(root)[0];

            Assert.That(entry.BasePath, Is.Empty);
        }

        [Test]
        public void CollectMergeAnimatorEntries_UsesObjectPath_ForRelativeMode()
        {
            var root = CreateAvatarRoot();
            var mergeAnimator = AddMergeAnimator(root, "Prefab", true, "Dummy");
            mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;

            var entry = AvatarEnvironmentScanner.CollectMergeAnimatorEntries(root)[0];

            Assert.That(entry.BasePath, Is.EqualTo("Prefab"));
        }

        [Test]
        public void CollectMergeAnimatorEntries_UsesRelativePathRoot_WhenSpecified()
        {
            // relativePathRootの解決はアバタールートを起点にするため、Descriptorが要る
            var root = CreateAvatarRoot();
            root.AddComponent<VRCAvatarDescriptor>();

            var target = new GameObject("Face");
            target.transform.SetParent(root.transform, false);

            var mergeAnimator = AddMergeAnimator(root, "Prefab", true, "Dummy");
            mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
            mergeAnimator.relativePathRoot.Set(target);

            var entry = AvatarEnvironmentScanner.CollectMergeAnimatorEntries(root)[0];

            Assert.That(entry.BasePath, Is.EqualTo("Face"));
        }

        [Test]
        public void CollectMergeAnimatorEntries_IgnoresMergeAnimatorWithoutController()
        {
            var root = CreateAvatarRoot();
            var child = new GameObject("Empty");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<ModularAvatarMergeAnimator>();

            Assert.That(AvatarEnvironmentScanner.CollectMergeAnimatorEntries(root), Is.Empty);
        }
    }
}
