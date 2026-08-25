using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// Override Controllerの解決を検証する
    /// </summary>
    public class AnimatorControllerResolverTests
    {
        private AnimatorController _controller;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _controller = new AnimatorController { name = "Base" };
            _controller.AddLayer("Layer");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created.Where(target => target != null))
            {
                Object.DestroyImmediate(target);
            }

            _created.Clear();

            if (_controller != null)
            {
                Object.DestroyImmediate(_controller);
            }
        }

        [Test]
        public void Resolve_ReturnsBaseController_ThroughNestedOverrides()
        {
            var inner = CreateOverride(_controller);
            var outer = CreateOverride(inner);

            Assert.That(AnimatorControllerResolver.Resolve(outer), Is.SameAs(_controller));
        }

        [Test]
        public void CollectOverrides_MapsOriginalClipToReplacement()
        {
            var original = AddState("State");
            var replacement = CreateClip("Replacement");

            var overrideController = CreateOverride(_controller);
            overrideController[original] = replacement;

            var map = AnimatorControllerResolver.CollectOverrides(overrideController);

            Assert.That(AnimatorControllerResolver.Apply(map, original), Is.SameAs(replacement));
        }

        /// <summary>
        /// Override Controllerを重ねても、鍵になるのは元のコントローラのクリップである
        /// </summary>
        /// <remarks>
        /// Override Controllerを別のOverride Controllerへ渡しても、階層は2段にならない。
        /// Unityが基底のAnimatorControllerまで解決するため、外側から見えるのは元のクリップだけで、
        /// 内側で指定した差し替えは残らない。
        /// </remarks>
        [Test]
        public void CollectOverrides_ReadsTheOutermostController_WhenNested()
        {
            var original = AddState("State");
            var middle = CreateClip("Middle");
            var last = CreateClip("Last");

            var inner = CreateOverride(_controller);
            inner[original] = middle;

            var outer = CreateOverride(inner);
            outer[original] = last;

            var map = AnimatorControllerResolver.CollectOverrides(outer);

            Assert.That(AnimatorControllerResolver.Apply(map, original), Is.SameAs(last));
        }

        [Test]
        public void Apply_KeepsClip_WhenNotOverridden()
        {
            var clip = CreateClip("Clip");
            var map = AnimatorControllerResolver.CollectOverrides(_controller);

            Assert.That(map, Is.Empty);
            Assert.That(AnimatorControllerResolver.Apply(map, clip), Is.SameAs(clip));
        }

        private AnimatorOverrideController CreateOverride(RuntimeAnimatorController baseController)
        {
            var overrideController = new AnimatorOverrideController(baseController);
            _created.Add(overrideController);

            return overrideController;
        }

        private AnimationClip AddState(string stateName)
        {
            var clip = CreateClip(stateName + " Clip");
            _controller.layers[0].stateMachine.AddState(stateName).motion = clip;

            return clip;
        }

        private AnimationClip CreateClip(string clipName)
        {
            var clip = new AnimationClip { name = clipName };
            _created.Add(clip);
            clip.SetCurve(
                "Body", typeof(SkinnedMeshRenderer), "blendShape." + clipName, AnimationCurve.Constant(0f, 1f, 100f));

            return clip;
        }
    }
}
