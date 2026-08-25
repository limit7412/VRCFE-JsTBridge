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
        public void Read_IgnoresBlendShapeNamedPropertiesOnOtherComponents()
        {
            _controller.AddLayer("Toggle");
            var state = _controller.layers[0].stateMachine.AddState("State");
            var clip = new AnimationClip { name = "Fake" };
            _created.Add(clip);

            // 同じ名前でも、SkinnedMeshRenderer以外に書くものはブレンドシェイプではない
            clip.SetCurve("Body", typeof(Light), "blendShape.Smile", AnimationCurve.Constant(0f, 1f, 1f));
            state.motion = clip;

            Assert.That(FxLayerSnapshotReader.Read(_controller).Single().BlendShapeBindings, Is.Empty);
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

        [Test]
        public void Read_ReadsSyncedLayer_ThroughOverrideMotion()
        {
            var sourceState = AddLayerWithClip("Source", "Body", "blendShape.Smile");

            // 同期レイヤー自身のステートマシンは再生されない
            AddLayerWithClip("Synced", "Body", "blendShape.NotPlayed");

            var layers = _controller.layers;
            layers[1].syncedLayerIndex = 0;
            layers[1].SetOverrideMotion(sourceState, CreateClip("Body", "blendShape.JawOpen"));
            _controller.layers = layers;

            var snapshots = FxLayerSnapshotReader.Read(_controller);

            Assert.That(snapshots[1].BlendShapeBindings, Is.EqualTo(new[] { "Body/blendShape.JawOpen" }));
        }

        [Test]
        public void Read_ReadsSyncedLayer_FromSourceMotion_WhenNotOverridden()
        {
            AddLayerWithClip("Source", "Body", "blendShape.Smile");
            _controller.AddLayer("Synced");

            var layers = _controller.layers;
            layers[1].syncedLayerIndex = 0;
            _controller.layers = layers;

            var snapshots = FxLayerSnapshotReader.Read(_controller);

            Assert.That(snapshots[1].BlendShapeBindings, Is.EqualTo(new[] { "Body/blendShape.Smile" }));
        }

        [Test]
        public void Read_DetectsTrackingControl_OnSyncedLayer()
        {
            _controller.AddLayer("Source");
            var state = _controller.layers[0].stateMachine.AddState("Fist");
            var control = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            control.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.Animation;

            _controller.AddLayer("Synced");
            var layers = _controller.layers;
            layers[1].syncedLayerIndex = 0;
            _controller.layers = layers;

            Assert.That(FxLayerSnapshotReader.Read(_controller)[1].ChangesTrackingControl, Is.True);
        }

        [Test]
        public void Read_FollowsOverrideController()
        {
            var state = AddLayerWithClip("A", "Body", "blendShape.Smile");
            var overrideController = new AnimatorOverrideController(_controller);
            _created.Add(overrideController);
            overrideController[(AnimationClip)state.motion] = CreateClip("Body", "blendShape.JawOpen");

            var snapshot = FxLayerSnapshotReader.Read(overrideController).Single();

            Assert.That(snapshot.BlendShapeBindings, Is.EqualTo(new[] { "Body/blendShape.JawOpen" }));
        }

        [Test]
        public void Read_FollowsSyncedLayerChain_ToItsSource()
        {
            AddLayerWithClip("Source", "Body", "blendShape.Smile");
            _controller.AddLayer("Middle");
            _controller.AddLayer("Last");

            var layers = _controller.layers;
            layers[1].syncedLayerIndex = 0;
            layers[2].syncedLayerIndex = 1;
            _controller.layers = layers;

            var snapshots = FxLayerSnapshotReader.Read(_controller);

            Assert.That(snapshots[2].BlendShapeBindings, Is.EqualTo(new[] { "Body/blendShape.Smile" }));
        }

        [Test]
        public void Read_StopsFollowingSyncedLayers_WhenTheyReferenceEachOther()
        {
            _controller.AddLayer("A");
            _controller.AddLayer("B");

            var layers = _controller.layers;
            layers[0].syncedLayerIndex = 1;
            layers[1].syncedLayerIndex = 0;
            _controller.layers = layers;

            Assert.That(FxLayerSnapshotReader.Read(_controller).Count, Is.EqualTo(2));
        }

        [Test]
        public void Read_IgnoresSourceBehaviours_WhenSyncedLayerOverridesThem()
        {
            _controller.AddLayer("Source");
            var state = _controller.layers[0].stateMachine.AddState("Fist");
            var control = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            control.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Animation;

            _controller.AddLayer("Synced");
            var layers = _controller.layers;
            layers[1].syncedLayerIndex = 0;

            // 差し替えたステートでは、同期元のbehaviourは動かない
            var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            _created.Add(driver);
            layers[1].SetOverrideBehaviours(state, new StateMachineBehaviour[] { driver });
            _controller.layers = layers;

            var snapshots = FxLayerSnapshotReader.Read(_controller);

            Assert.That(snapshots[0].ChangesTrackingControl, Is.True);
            Assert.That(snapshots[1].ChangesTrackingControl, Is.False);
        }

        [Test]
        public void Read_DetectsWriteDefaults()
        {
            var state = AddLayerWithClip("Toggle", "Body", "blendShape.Smile");
            state.writeDefaultValues = true;

            Assert.That(FxLayerSnapshotReader.Read(_controller).Single().HasWriteDefaults, Is.True);
        }

        [Test]
        public void Read_ReportsNoWriteDefaults_WhenEveryStateHasThemOff()
        {
            var state = AddLayerWithClip("Toggle", "Body", "blendShape.Smile");
            state.writeDefaultValues = false;

            Assert.That(FxLayerSnapshotReader.Read(_controller).Single().HasWriteDefaults, Is.False);
        }

        [Test]
        public void CollectBlendShapeBindings_FollowsOverrideController()
        {
            var state = AddLayerWithClip("A", "Body", "blendShape.Smile");
            var overrideController = new AnimatorOverrideController(_controller);
            _created.Add(overrideController);
            overrideController[(AnimationClip)state.motion] = CreateClip("Body", "blendShape.JawOpen");

            Assert.That(
                FxLayerSnapshotReader.CollectBlendShapeBindings(overrideController),
                Is.EqualTo(new[] { "Body/blendShape.JawOpen" }));
        }

        [Test]
        public void CollectBlendShapeBindings_PrependsBasePath()
        {
            AddLayerWithClip("A", "Body", "blendShape.Smile");

            Assert.That(
                FxLayerSnapshotReader.CollectBlendShapeBindings(_controller, "Prefab/Face"),
                Is.EqualTo(new[] { "Prefab/Face/Body/blendShape.Smile" }));
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
