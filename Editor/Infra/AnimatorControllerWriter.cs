using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using FEJsTBridge.Domain;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// 生成計画をAnimatorControllerへ実体化する
    /// 計画の内容をそのまま写すだけで、閾値や値の判断は持たない
    /// </summary>
    internal static class AnimatorControllerWriter
    {
        public const string ControllerName = "FEJsTBridge Generated";

        /// <summary>
        /// 空クリップが束縛するダミーのオブジェクトパス
        ///
        /// クリップの長さはカーブからしか決まらないため、待ち時間を持つステートのために
        /// 束縛先のないカーブを1本だけ置く。アバターに同名のオブジェクトが現れない限り、
        /// このカーブは実行時に何も動かさない。
        /// </summary>
        private const string DummyBindingPath = "FEJsTBridge_EmptyClipBinding";

        /// <summary>
        /// 計画からコントローラを組み立てる
        /// </summary>
        /// <param name="plan">生成計画</param>
        /// <param name="saveAsset">
        /// 生成したアセットをビルドの一時アセットへ登録するコールバック。
        /// コントローラを先に登録すると、以降に生成されるステートマシンやステートは
        /// Unity側が同じアセットへ自動で追加する
        /// </param>
        public static AnimatorController Write(BridgeControllerPlan plan, Action<Object> saveAsset = null)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var controller = new AnimatorController { name = ControllerName };
            saveAsset?.Invoke(controller);

            foreach (var parameter in plan.Parameters)
            {
                controller.AddParameter(parameter.Name, ToUnityParameterType(parameter.Type));
            }

            // 同じ長さの空クリップはコントローラ内で共有する
            var clipCache = new Dictionary<float, AnimationClip>();

            foreach (var layerPlan in plan.Layers)
            {
                controller.AddLayer(layerPlan.Name);
                var layers = controller.layers;
                var stateMachine = layers[layers.Length - 1].stateMachine;

                WriteLayer(layerPlan, stateMachine, clipCache, saveAsset);
            }

            // AddLayerで作られるレイヤーの重みは0のため、明示的に1へ揃える。
            // layersはコピーを返すプロパティなので、書き戻すまで反映されない
            var allLayers = controller.layers;
            foreach (var layer in allLayers)
            {
                layer.defaultWeight = 1f;
            }

            controller.layers = allLayers;

            return controller;
        }

        private static void WriteLayer(
            BridgeLayerPlan layerPlan,
            AnimatorStateMachine stateMachine,
            Dictionary<float, AnimationClip> clipCache,
            Action<Object> saveAsset)
        {
            var states = new Dictionary<string, AnimatorState>();

            foreach (var statePlan in layerPlan.States)
            {
                var state = stateMachine.AddState(statePlan.Name);

                // Merge Animatorのmatch avatar write defaultsで環境に合わせるため、
                // 生成時点の値はどちらでもよい。ブリッジはブレンドシェイプを書かない
                state.writeDefaultValues = true;
                state.motion = GetOrCreateEmptyClip(statePlan.MotionLengthSeconds, clipCache, saveAsset);

                WriteDriver(state, statePlan.Driver);
                WriteTrackingControl(state, statePlan.TrackingControl);

                states[statePlan.Name] = state;
            }

            stateMachine.defaultState = states[layerPlan.DefaultStateName];

            foreach (var transitionPlan in layerPlan.Transitions)
            {
                var transition = states[transitionPlan.From].AddTransition(states[transitionPlan.To]);
                transition.hasExitTime = transitionPlan.HasExitTime;
                transition.exitTime = transitionPlan.ExitTime;
                transition.hasFixedDuration = true;
                transition.duration = 0f;
                transition.offset = 0f;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.canTransitionToSelf = false;

                foreach (var condition in transitionPlan.Conditions)
                {
                    transition.AddCondition(
                        ToUnityConditionMode(condition.Mode),
                        condition.Threshold,
                        condition.Parameter);
                }
            }
        }

        private static void WriteDriver(AnimatorState state, BridgeDriverPlan driverPlan)
        {
            if (driverPlan == null)
            {
                return;
            }

            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = driverPlan.LocalOnly;
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>();

            foreach (var entry in driverPlan.Entries)
            {
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = entry.Parameter,
                    value = entry.Value,
                });
            }
        }

        private static void WriteTrackingControl(AnimatorState state, BridgeTrackingControlPlan trackingPlan)
        {
            if (trackingPlan == null)
            {
                return;
            }

            var trackingControl = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            trackingControl.trackingEyes = ToUnityTrackingType(trackingPlan.Eyes);
            trackingControl.trackingMouth = ToUnityTrackingType(trackingPlan.Mouth);
        }

        private static AnimationClip GetOrCreateEmptyClip(
            float lengthSeconds,
            Dictionary<float, AnimationClip> clipCache,
            Action<Object> saveAsset)
        {
            if (clipCache.TryGetValue(lengthSeconds, out var cached))
            {
                return cached;
            }

            var clip = new AnimationClip { name = $"FEJsTBridge Empty {lengthSeconds:0.###}s" };
            clip.SetCurve(
                DummyBindingPath,
                typeof(Transform),
                "m_LocalPosition.x",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(lengthSeconds, 0f)));

            saveAsset?.Invoke(clip);
            clipCache[lengthSeconds] = clip;

            return clip;
        }

        private static AnimatorControllerParameterType ToUnityParameterType(BridgeParameterType type)
        {
            switch (type)
            {
                case BridgeParameterType.Bool:
                    return AnimatorControllerParameterType.Bool;
                case BridgeParameterType.Float:
                    return AnimatorControllerParameterType.Float;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static AnimatorConditionMode ToUnityConditionMode(BridgeConditionMode mode)
        {
            switch (mode)
            {
                case BridgeConditionMode.If:
                    return AnimatorConditionMode.If;
                case BridgeConditionMode.IfNot:
                    return AnimatorConditionMode.IfNot;
                case BridgeConditionMode.Greater:
                    return AnimatorConditionMode.Greater;
                case BridgeConditionMode.Less:
                    return AnimatorConditionMode.Less;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static VRC_AnimatorTrackingControl.TrackingType ToUnityTrackingType(BridgeTrackingState state)
        {
            switch (state)
            {
                case BridgeTrackingState.Tracking:
                    return VRC_AnimatorTrackingControl.TrackingType.Tracking;
                case BridgeTrackingState.Animation:
                    return VRC_AnimatorTrackingControl.TrackingType.Animation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}
