using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    public class FxLayerSnapshotReaderTests
    {
        private AnimatorController _controller;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _controller = new AnimatorController { name = "FX" };
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
        public void Read_CollectsBlendShapeBindings_FromClips()
        {
            AddLayerWithClip("Left Hand Face", "Body", "blendShape.Smile");

            var snapshot = FxLayerSnapshotReader.Read(_controller).Single();

            Assert.That(snapshot.Name, Is.EqualTo("Left Hand Face"));
            Assert.That(snapshot.Index, Is.EqualTo(0));
            Assert.That(snapshot.BlendShapeBindings, Is.EqualTo(new[] { "Body/blendShape.Smile" }));
        }

        [Test]
        public void Read_IgnoresCurvesThatAreNotBlendShapes()
        {
            var state = AddLayerWithClip("Toggle", "Body", "blendShape.Smile");
            ((AnimationClip)state.motion).SetCurve(
                "Hat", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0f, 1f, 1f));

            var snapshot = FxLayerSnapshotReader.Read(_controller).Single();

            Assert.That(snapshot.BlendShapeBindings, Is.EqualTo(new[] { "Body/blendShape.Smile" }));
        }

        [Test]
        public void Read_LooksInsideBlendTrees()
        {
            var clip = CreateClip("Body", "blendShape.JawOpen");
            var tree = new BlendTree { name = "Tree" };
            _created.Add(tree);
            tree.AddChild(clip);

            _controller.AddLayer("Mouth Control");
            var state = _controller.layers[0].stateMachine.AddState("Blend");
            state.motion = tree;

            var snapshot = FxLayerSnapshotReader.Read(_controller).Single();

            Assert.That(snapshot.BlendShapeBindings, Is.EqualTo(new[] { "Body/blendShape.JawOpen" }));
        }

        [Test]
        public void Read_DetectsTrackingControl()
        {
            _controller.AddLayer("Right Hand Tracking Control");
            var state = _controller.layers[0].stateMachine.AddState("Fist");
            var control = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            control.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Animation;

            Assert.That(FxLayerSnapshotReader.Read(_controller).Single().ChangesTrackingControl, Is.True);
        }

        [Test]
        public void Read_IgnoresTrackingControlThatChangesNothing()
        {
            _controller.AddLayer("Something");
            var state = _controller.layers[0].stateMachine.AddState("State");
            state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();

            Assert.That(FxLayerSnapshotReader.Read(_controller).Single().ChangesTrackingControl, Is.False);
        }

        [Test]
        public void CollectBlendShapeBindings_GathersEveryLayer()
        {
            AddLayerWithClip("A", "Body", "blendShape.Smile");
            AddLayerWithClip("B", "Body", "blendShape.JawOpen");

            Assert.That(
                FxLayerSnapshotReader.CollectBlendShapeBindings(_controller).OrderBy(binding => binding),
                Is.EqualTo(new[] { "Body/blendShape.JawOpen", "Body/blendShape.Smile" }));
        }

        [Test]
        public void CollectBlendShapeBindings_ReturnsEmpty_ForNull()
        {
            Assert.That(FxLayerSnapshotReader.CollectBlendShapeBindings(null), Is.Empty);
        }

        private AnimatorState AddLayerWithClip(string layerName, string path, string property)
        {
            _controller.AddLayer(layerName);
            var stateMachine = _controller.layers[_controller.layers.Length - 1].stateMachine;
            var state = stateMachine.AddState("State");
            state.motion = CreateClip(path, property);

            return state;
        }

        private AnimationClip CreateClip(string path, string property)
        {
            var clip = new AnimationClip { name = property };
            _created.Add(clip);
            clip.SetCurve(path, typeof(SkinnedMeshRenderer), property, AnimationCurve.Constant(0f, 1f, 100f));

            return clip;
        }
    }
}
