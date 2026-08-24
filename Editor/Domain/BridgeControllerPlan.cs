using System.Collections.Generic;
using System.Linq;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 生成するAnimatorControllerの仕様をそのまま表すデータ構造
    ///
    /// 閾値やDriverの値といった判断はすべてBridgePlanBuilderが行い、
    /// AnimatorControllerWriterはこの計画を機械的に実体化するだけになる。
    /// テストは計画に対して書けるため、大半がUnityのアニメーターAPIに触れずに済む。
    /// </summary>
    internal sealed class BridgeControllerPlan
    {
        public BridgeControllerPlan(
            IReadOnlyList<BridgeParameterPlan> parameters,
            IReadOnlyList<BridgeLayerPlan> layers)
        {
            Parameters = parameters;
            Layers = layers;
        }

        public IReadOnlyList<BridgeParameterPlan> Parameters { get; }

        public IReadOnlyList<BridgeLayerPlan> Layers { get; }

        public BridgeLayerPlan FindLayer(string name)
        {
            return Layers.FirstOrDefault(layer => layer.Name == name);
        }
    }

    /// <summary>コントローラに宣言するパラメータ</summary>
    internal sealed class BridgeParameterPlan
    {
        public BridgeParameterPlan(string name, BridgeParameterType type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }

        public BridgeParameterType Type { get; }
    }

    internal enum BridgeParameterType
    {
        Bool,
        Float
    }

    /// <summary>レイヤー1つ分の定義</summary>
    internal sealed class BridgeLayerPlan
    {
        public BridgeLayerPlan(
            string name,
            string defaultStateName,
            IReadOnlyList<BridgeStatePlan> states,
            IReadOnlyList<BridgeTransitionPlan> transitions)
        {
            Name = name;
            DefaultStateName = defaultStateName;
            States = states;
            Transitions = transitions;
        }

        public string Name { get; }

        public string DefaultStateName { get; }

        public IReadOnlyList<BridgeStatePlan> States { get; }

        /// <summary>
        /// レイヤー内の全遷移。同じ遷移元を持つものは記載順がそのまま優先順位になる
        /// </summary>
        public IReadOnlyList<BridgeTransitionPlan> Transitions { get; }

        public BridgeStatePlan FindState(string name)
        {
            return States.FirstOrDefault(state => state.Name == name);
        }

        public IReadOnlyList<BridgeTransitionPlan> TransitionsFrom(string stateName)
        {
            return Transitions.Where(transition => transition.From == stateName).ToArray();
        }
    }

    /// <summary>ステート1つ分の定義</summary>
    internal sealed class BridgeStatePlan
    {
        public BridgeStatePlan(
            string name,
            float motionLengthSeconds,
            BridgeDriverPlan driver = null,
            BridgeTrackingControlPlan trackingControl = null)
        {
            Name = name;
            MotionLengthSeconds = motionLengthSeconds;
            Driver = driver;
            TrackingControl = trackingControl;
        }

        public string Name { get; }

        /// <summary>割り当てる空クリップの長さ。同じ長さのクリップはコントローラ内で共有される</summary>
        public float MotionLengthSeconds { get; }

        public BridgeDriverPlan Driver { get; }

        public BridgeTrackingControlPlan TrackingControl { get; }
    }

    /// <summary>VRCAvatarParameterDriverの定義</summary>
    internal sealed class BridgeDriverPlan
    {
        public BridgeDriverPlan(bool localOnly, IReadOnlyList<BridgeDriverEntry> entries)
        {
            LocalOnly = localOnly;
            Entries = entries;
        }

        /// <summary>
        /// falseで生成する。同期済みの入力から導出するため、リモートでもDriverを走らせて
        /// 各クライアントが同じ結果へ到達させる
        /// </summary>
        public bool LocalOnly { get; }

        public IReadOnlyList<BridgeDriverEntry> Entries { get; }
    }

    /// <summary>Driverが書き込むパラメータと値の組</summary>
    internal sealed class BridgeDriverEntry
    {
        public BridgeDriverEntry(string parameter, float value)
        {
            Parameter = parameter;
            Value = value;
        }

        public string Parameter { get; }

        public float Value { get; }
    }

    /// <summary>VRCAnimatorTrackingControlの定義</summary>
    internal sealed class BridgeTrackingControlPlan
    {
        public BridgeTrackingControlPlan(BridgeTrackingState eyes, BridgeTrackingState mouth)
        {
            Eyes = eyes;
            Mouth = mouth;
        }

        public BridgeTrackingState Eyes { get; }

        public BridgeTrackingState Mouth { get; }
    }

    internal enum BridgeTrackingState
    {
        Tracking,
        Animation
    }

    /// <summary>遷移1本分の定義</summary>
    internal sealed class BridgeTransitionPlan
    {
        public BridgeTransitionPlan(
            string from,
            string to,
            IReadOnlyList<BridgeConditionPlan> conditions,
            bool hasExitTime = false,
            float exitTime = 0f)
        {
            From = from;
            To = to;
            Conditions = conditions;
            HasExitTime = hasExitTime;
            ExitTime = exitTime;
        }

        public string From { get; }

        public string To { get; }

        public IReadOnlyList<BridgeConditionPlan> Conditions { get; }

        public bool HasExitTime { get; }

        /// <summary>正規化時間で表した発火位置。HasExitTimeがfalseのときは参照されない</summary>
        public float ExitTime { get; }
    }

    /// <summary>遷移条件。パラメータ名、比較モード、閾値の3つ組</summary>
    internal sealed class BridgeConditionPlan
    {
        public BridgeConditionPlan(string parameter, BridgeConditionMode mode, float threshold = 0f)
        {
            Parameter = parameter;
            Mode = mode;
            Threshold = threshold;
        }

        public string Parameter { get; }

        public BridgeConditionMode Mode { get; }

        /// <summary>Greater・Lessでのみ使う閾値。If・IfNotでは0を入れる</summary>
        public float Threshold { get; }
    }

    internal enum BridgeConditionMode
    {
        If,
        IfNot,
        Greater,
        Less
    }
}
