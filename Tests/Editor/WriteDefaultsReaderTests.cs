using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;
using FEJsTBridge.Domain;
using FEJsTBridge.Infra;
using FEJsTBridge.UseCase;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// Write Defaultsの読み取りと、それを使ったバイパス方式の判定を検証する
    /// </summary>
    public class WriteDefaultsReaderTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created.Where(target => target != null))
            {
                Object.DestroyImmediate(target);
            }

            _created.Clear();
        }

        private AnimatorController CreateController(string name)
        {
            var controller = new AnimatorController { name = name };
            _created.Add(controller);
            return controller;
        }

        private static AnimatorState AddLayer(AnimatorController controller, string layerName, bool writeDefaults)
        {
            controller.AddLayer(layerName);
            var state = controller.layers[controller.layers.Length - 1].stateMachine.AddState("State");
            state.writeDefaultValues = writeDefaults;

            return state;
        }

        /// <summary>
        /// Directブレンドツリーを1ステートだけで再生するレイヤーを足す
        /// </summary>
        private void AddDirectBlendTreeLayer(AnimatorController controller, string layerName)
        {
            var blendTree = new BlendTree { name = "Direct", blendType = BlendTreeType.Direct };
            _created.Add(blendTree);

            AddLayer(controller, layerName, writeDefaults: true).motion = blendTree;
        }

        private GameObject CreateAvatar(AnimatorController fx)
        {
            var avatarRoot = new GameObject("Avatar");
            _created.Add(avatarRoot);

            var descriptor = avatarRoot.AddComponent<VRCAvatarDescriptor>();
            descriptor.baseAnimationLayers = new[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    animatorController = fx,
                    isDefault = false,
                    isEnabled = true,
                },
            };
            descriptor.specialAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];

            return avatarRoot;
        }

        private AnimatorController AddFaceEmo(GameObject avatarRoot, bool writeDefaults, bool matchAvatar)
        {
            var controller = CreateController("FaceEmo");
            controller.AddParameter(BridgeParameterNames.ForceBypassEnable, AnimatorControllerParameterType.Bool);
            AddLayer(controller, "DEFAULT FACE", writeDefaults);

            var holder = new GameObject("FaceEmoPrefab");
            holder.transform.SetParent(avatarRoot.transform, false);

            var mergeAnimator = holder.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = matchAvatar;

            return controller;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReadUniform_ReturnsValue_WhenEveryStateAgrees(bool writeDefaults)
        {
            var controller = CreateController("FX");
            AddLayer(controller, "A", writeDefaults);
            AddLayer(controller, "B", writeDefaults);

            Assert.That(WriteDefaultsReader.ReadUniform(controller, null), Is.EqualTo(writeDefaults));
        }

        [Test]
        public void ReadUniform_ReturnsNull_WhenStatesDisagree()
        {
            var controller = CreateController("FX");
            AddLayer(controller, "A", writeDefaults: true);
            AddLayer(controller, "B", writeDefaults: false);

            Assert.That(WriteDefaultsReader.ReadUniform(controller, null), Is.Null);
        }

        [Test]
        public void ReadUniform_ReturnsNull_WhenControllerHasNoState()
        {
            Assert.That(WriteDefaultsReader.ReadUniform(CreateController("FX"), null), Is.Null);
            Assert.That(WriteDefaultsReader.ReadUniform(null, null), Is.Null);
        }

        /// <summary>
        /// ビルド時に取り除くレイヤーは、マージの時点ではFXに居ない
        /// </summary>
        [Test]
        public void ReadUniform_SkipsIgnoredLayers()
        {
            var controller = CreateController("FX");
            AddLayer(controller, "Toggle", writeDefaults: false);
            AddLayer(controller, " Left Hand ", writeDefaults: true);

            Assert.That(WriteDefaultsReader.ReadUniform(controller, new[] { "Left Hand" }), Is.False);
        }

        /// <summary>
        /// Write Defaultsを有効にするしかないレイヤーは、Modular Avatarも判定に数えない
        /// </summary>
        [Test]
        public void ReadUniform_SkipsLayersThatRequireWriteDefaults()
        {
            var controller = CreateController("FX");
            AddLayer(controller, "Toggle", writeDefaults: false);
            AddDirectBlendTreeLayer(controller, "Direct");

            AddLayer(controller, "Additive", writeDefaults: true);
            var layers = controller.layers;
            layers[2].blendingMode = AnimatorLayerBlendingMode.Additive;
            controller.layers = layers;

            Assert.That(WriteDefaultsReader.ReadUniform(controller, null), Is.False);
        }

        [Test]
        public void HasWriteDefaultsOffState_FindsStateWithWriteDefaultsOff()
        {
            var controller = CreateController("FaceEmo");
            AddLayer(controller, "A", writeDefaults: true);
            Assert.That(WriteDefaultsReader.HasWriteDefaultsOffState(controller), Is.False);

            AddLayer(controller, "B", writeDefaults: false);
            Assert.That(WriteDefaultsReader.HasWriteDefaultsOffState(controller), Is.True);
        }

        [Test]
        public void DetectFaceLeftOnBypass_IsTrue_WhenFaceEmoFollowsAvatarWithWriteDefaultsOff()
        {
            var fx = CreateController("FX");
            AddLayer(fx, "Toggle", writeDefaults: false);
            var avatarRoot = CreateAvatar(fx);
            AddFaceEmo(avatarRoot, writeDefaults: true, matchAvatar: true);

            Assert.That(GenerateBridgeUseCase.DetectFaceLeftOnBypass(avatarRoot, new string[0]), Is.True);
        }

        [Test]
        public void DetectFaceLeftOnBypass_IsFalse_WhenFaceEmoRunsWithWriteDefaultsOn()
        {
            var fx = CreateController("FX");
            AddLayer(fx, "Toggle", writeDefaults: true);
            var avatarRoot = CreateAvatar(fx);
            AddFaceEmo(avatarRoot, writeDefaults: true, matchAvatar: true);

            Assert.That(GenerateBridgeUseCase.DetectFaceLeftOnBypass(avatarRoot, new string[0]), Is.False);
        }

        /// <summary>
        /// 取り除くレイヤーを数えると、アバターのWrite Defaultsが混ざって見え、判定が変わる
        /// </summary>
        [Test]
        public void DetectFaceLeftOnBypass_IgnoresLayersRemovedAtBuild()
        {
            var fx = CreateController("FX");
            AddLayer(fx, "Toggle", writeDefaults: false);
            AddLayer(fx, "Left Hand", writeDefaults: true);
            var avatarRoot = CreateAvatar(fx);
            AddFaceEmo(avatarRoot, writeDefaults: true, matchAvatar: true);

            Assert.That(GenerateBridgeUseCase.DetectFaceLeftOnBypass(avatarRoot, new string[0]), Is.False);
            Assert.That(GenerateBridgeUseCase.DetectFaceLeftOnBypass(avatarRoot, new[] { "Left Hand" }), Is.True);
        }

        [Test]
        public void DetectFaceLeftOnBypass_IsFalse_WhenFaceEmoIsAbsent()
        {
            var fx = CreateController("FX");
            AddLayer(fx, "Toggle", writeDefaults: false);

            Assert.That(GenerateBridgeUseCase.DetectFaceLeftOnBypass(CreateAvatar(fx), new string[0]), Is.False);
        }
    }
}
