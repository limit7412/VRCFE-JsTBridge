using System.Collections.Generic;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 設定値から生成計画を組み立てる
    /// 生成物仕様の実体はすべてここにあり、他の層は判断を持たない
    /// </summary>
    internal static class BridgePlanBuilder
    {
        public const string BypassLayerName = "BypassBridge";
        public const string ExpressionControlLayerName = "ExpressionControl";
        public const string TrackingReapplyLayerName = "TrackingReapply";
        public const string ExtraParametersLayerName = "ExtraParameters";
        public const string UnsyncedExtraParametersLayerName = "ExtraParametersUnsynced";

        public const string IdleStateName = "Idle";
        public const string BypassStateName = "Bypass";
        public const string EngagedStateName = "Engaged";
        public const string RedriveStateName = "Redrive";
        public const string WaitStateName = "Wait";
        public const string ArmedStateName = "Armed";
        public const string InitialStateName = "Initial";
        public const string ReleasedStateName = "Released";
        public const string ReassertStateName = "Reassert";

        /// <summary>Armed以外の全ステートで共有する空クリップの長さ</summary>
        public const float DefaultClipLengthSeconds = 1.0f;

        /// <summary>
        /// Float型トリガーの判定閾値
        /// EyeTrackingActiveとLipTrackingActiveは0か1しか取らないため、
        /// 0.5で切ればJerryの0.992 / 0.008による判定と等価になる
        /// </summary>
        private const float FloatTriggerThreshold = 0.5f;

        /// <summary>
        /// FaceEmoのパラメータ名を解決せずに組み立てる
        /// バイパス方式は解決の対象にならないパラメータしか使わないため、これで足りる
        /// </summary>
        public static BridgeControllerPlan Build(BridgeSettings settings)
        {
            return Build(settings, FaceEmoParameterNames.Raw);
        }

        public static BridgeControllerPlan Build(BridgeSettings settings, FaceEmoParameterNames faceEmo)
        {
            var parameters = new List<BridgeParameterPlan>
            {
                new BridgeParameterPlan(BridgeParameterNames.FacialExpressionsDisabled, BridgeParameterType.Bool),
                new BridgeParameterPlan(BridgeParameterNames.LipTrackingActive, BridgeParameterType.Float),
                new BridgeParameterPlan(BridgeParameterNames.EyeTrackingActive, BridgeParameterType.Float),
                new BridgeParameterPlan(BridgeParameterNames.VisemesEnable, BridgeParameterType.Bool),
            };

            var layers = new List<BridgeLayerPlan>();

            // 出力先のパラメータは方式ごとに違うため、使う分だけを宣言する。
            // Merge Animatorはコントローラのパラメータリストごとマージするので、
            // 使わないパラメータを宣言してもFX側の定義が増えるだけになる
            if (settings.ControlMethod == ControlMethod.ExpressionControl)
            {
                parameters.Add(new BridgeParameterPlan(faceEmo.EmoteLockEnable, BridgeParameterType.Bool));
                parameters.Add(new BridgeParameterPlan(faceEmo.ForceBlinkDisable, BridgeParameterType.Bool));
                parameters.Add(new BridgeParameterPlan(faceEmo.Emote, BridgeParameterType.Int));
                layers.Add(BuildExpressionControlLayer(settings, faceEmo));
            }
            else
            {
                parameters.Add(
                    new BridgeParameterPlan(BridgeParameterNames.ForceBypassEnable, BridgeParameterType.Bool));
                layers.Add(BuildBypassLayer(settings));
            }

            if (settings.EnableTrackingReapply)
            {
                layers.Add(BuildTrackingReapplyLayer(settings));
            }

            // 追加パラメータが無ければレイヤーを作らない。
            // この設定を持たない版で組んだアバターの生成物を変えないためである
            var extraParameters = settings.ExtraParameters;
            if (extraParameters != null && extraParameters.Count > 0)
            {
                DeclareExtraParameters(parameters, extraParameters);

                // 同期の扱いでDriverのlocalOnlyと書き直しの要否が変わるため、レイヤーを分ける
                var synced = new List<ExtraParameterTarget>();
                var unsynced = new List<ExtraParameterTarget>();
                foreach (var target in extraParameters)
                {
                    (target.Synced ? synced : unsynced).Add(target);
                }

                if (synced.Count > 0)
                {
                    layers.Add(BuildExtraParametersLayer(settings, synced));
                }

                if (unsynced.Count > 0)
                {
                    layers.Add(BuildUnsyncedExtraParametersLayer(settings, unsynced));
                }
            }

            return new BridgeControllerPlan(parameters, layers);
        }

        /// <summary>
        /// トリガーをCN_FORCE_BYPASS_ENABLEへ写すレイヤー
        ///
        /// 写しは一度きりにせず、空クリップを1周するたびにRedriveを経由して
        /// 元のステートへ入り直し、Driverを発火し直す。アバターのロード中は
        /// Driverの書き込みが失われることがあり、ステートが目的地に着いたまま
        /// 値だけが既定へ戻ると、装着者がトラッキングを切り替え直すまで
        /// バイパスが成立しない。FaceEmo側の遷移はこの値そのものを条件に持つため、
        /// 書き直しさえ届けば以降の連鎖はFaceEmoの中で復旧する。
        /// 同じ値の書き直しは無害で、非同期パラメータのため同期帯域も使わない。
        ///
        /// 入り直しに自己遷移を使わないのは、通常遷移の自己遷移で突入時の挙動が
        /// 再実行されるかをUnityが仕様として明言していないため。別ステートを
        /// 経由する形はTrackingReapplyのArmedと同じ、確実に動く構造である。
        /// </summary>
        private static BridgeLayerPlan BuildBypassLayer(BridgeSettings settings)
        {
            var states = new[]
            {
                new BridgeStatePlan(
                    IdleStateName,
                    DefaultClipLengthSeconds,
                    driver: BypassDriver(0f)),
                new BridgeStatePlan(
                    BypassStateName,
                    DefaultClipLengthSeconds,
                    driver: BypassDriver(1f)),
                new BridgeStatePlan(RedriveStateName, DefaultClipLengthSeconds),
            };

            var transitions = new[]
            {
                new BridgeTransitionPlan(IdleStateName, BypassStateName, new[] { TriggerOn(settings) }),
                new BridgeTransitionPlan(BypassStateName, IdleStateName, new[] { TriggerOff(settings) }),

                // 空クリップを1周したらRedriveへ抜ける。条件付きの遷移を記載順の
                // 先に置いてあるため、トリガーの切替はこのループより優先される
                new BridgeTransitionPlan(
                    IdleStateName,
                    RedriveStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f),
                new BridgeTransitionPlan(
                    BypassStateName,
                    RedriveStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f),

                // Redriveはトリガーの現在値に合うステートへ即座に戻る中継で、
                // 戻った先の突入でDriverが発火し直す。滞在は実質1フレームのため、
                // トリガー切替への応答は経由してもほぼ遅れない
                new BridgeTransitionPlan(RedriveStateName, BypassStateName, new[] { TriggerOn(settings) }),
                new BridgeTransitionPlan(RedriveStateName, IdleStateName, new[] { TriggerOff(settings) }),
            };

            return new BridgeLayerPlan(BypassLayerName, IdleStateName, states, transitions);
        }

        /// <summary>
        /// FaceEmoを動かしたまま、表情の書き込みを無害な状態へ寄せるレイヤー
        ///
        /// 表情ロックでジェスチャーによる表情の切り替えを止め、まばたきを強制的に停止し、
        /// メニューの表情選択と同じパラメータで指定の表情へ切り替える。
        /// FaceEmoは書き込みを続けるため、FaceEmoより前にいる素体の表情レイヤーは
        /// 通常時と同じく押さえ込まれたままになる。
        ///
        /// バイパス方式と違い、周期的な書き直しは行わない。
        /// 書き込む先がすべて同期パラメータであり、値そのものが後からjoinした人へ届くためである。
        /// 書き直し続けると、装着者がメニューで選び直した表情を毎周期奪ってしまう。
        ///
        /// Driverはlocal onlyで生成する。同期パラメータをリモートでも書くと、
        /// 書き込んだ値と届いた同期値が競合する。FaceEmo自身も同じ理由でローカル駆動している。
        /// </summary>
        private static BridgeLayerPlan BuildExpressionControlLayer(
            BridgeSettings settings, FaceEmoParameterNames faceEmo)
        {
            var states = new[]
            {
                new BridgeStatePlan(
                    IdleStateName,
                    DefaultClipLengthSeconds,
                    driver: ReleaseDriver(faceEmo)),
                new BridgeStatePlan(
                    EngagedStateName,
                    DefaultClipLengthSeconds,
                    driver: EngageDriver(faceEmo, settings.FaceEmoteIndex)),
            };

            var transitions = new[]
            {
                new BridgeTransitionPlan(IdleStateName, EngagedStateName, new[] { TriggerOn(settings) }),
                new BridgeTransitionPlan(EngagedStateName, IdleStateName, new[] { TriggerOff(settings) }),
            };

            return new BridgeLayerPlan(ExpressionControlLayerName, IdleStateName, states, transitions);
        }

        /// <summary>
        /// FaceEmoの適用のあとにTracking ControlをJerryの状態へ合わせ直すレイヤー
        ///
        /// JerryとFaceEmoはどちらもステート突入時にTracking Controlを一度だけ適用する。
        /// バイパス方式では、バイパスがDriverの連鎖で成立するぶんFaceEmo側の適用が必ず後になり、
        /// Jerryの適用を上書きしてしまう。表情制御方式ではFaceEmoが動き続けるため、
        /// 表情が切り替わるたびに同じ上書きが起きる。どちらもさらに後から適用し直して直す。
        ///
        /// 適用し直しは一度きりにせず、トリガーが立っている間はApplyからArmedへ周期的に戻して繰り返す。
        /// バイパス方式では、後からjoinした人のクライアントでアバターのロード中にフレームが大きく落ち、
        /// FaceEmo側の適用がReapplyDelaySecondsを超えて遅れることがある。
        /// 表情制御方式では、装着者がメニューで表情を選び直すたびに追従がいる。
        /// 一度きりだと逆転した適用順のまま残るが、同じ値のTracking Controlの再適用は
        /// 見た目を変えないため、繰り返しても既にいる人には影響しない。
        /// </summary>
        private static BridgeLayerPlan BuildTrackingReapplyLayer(BridgeSettings settings)
        {
            var states = new List<BridgeStatePlan>
            {
                new BridgeStatePlan(WaitStateName, DefaultClipLengthSeconds),
                new BridgeStatePlan(ArmedStateName, settings.ReapplyDelaySeconds),
            };

            foreach (var (eye, visemes) in TrackingCombinations())
            {
                states.Add(new BridgeStatePlan(
                    ApplyStateName(eye, visemes),
                    DefaultClipLengthSeconds,
                    trackingControl: new BridgeTrackingControlPlan(
                        eyes: eye ? BridgeTrackingState.Animation : BridgeTrackingState.Tracking,
                        mouth: visemes ? BridgeTrackingState.Tracking : BridgeTrackingState.Animation)));
            }

            var transitions = new List<BridgeTransitionPlan>
            {
                new BridgeTransitionPlan(WaitStateName, ArmedStateName, new[] { TriggerOn(settings) }),
            };

            // Armedの空クリップを1周するまで発火しないため、Exit Timeが待ち時間そのものになる
            foreach (var (eye, visemes) in TrackingCombinations())
            {
                transitions.Add(new BridgeTransitionPlan(
                    ArmedStateName,
                    ApplyStateName(eye, visemes),
                    new[] { EyeCondition(eye), VisemesCondition(visemes) },
                    hasExitTime: true,
                    exitTime: 1.0f));
            }

            transitions.Add(new BridgeTransitionPlan(ArmedStateName, WaitStateName, new[] { TriggerOff(settings) }));

            // バイパス継続中に目の有効化やVisemesのトグルがあっても追従して再適用する
            foreach (var (eye, visemes) in TrackingCombinations())
            {
                var from = ApplyStateName(eye, visemes);

                transitions.Add(new BridgeTransitionPlan(
                    from,
                    ApplyStateName(!eye, visemes),
                    new[] { EyeCondition(!eye) }));

                transitions.Add(new BridgeTransitionPlan(
                    from,
                    ApplyStateName(eye, !visemes),
                    new[] { VisemesCondition(!visemes) }));

                transitions.Add(new BridgeTransitionPlan(
                    from,
                    WaitStateName,
                    new[] { TriggerOff(settings) }));

                // 空クリップを1周したらArmedへ戻り、Tracking Controlを適用し直す。
                // 条件付きの3本を先に並べてあるため、切替追従と解除はこのループより優先される
                transitions.Add(new BridgeTransitionPlan(
                    from,
                    ArmedStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f));
            }

            return new BridgeLayerPlan(TrackingReapplyLayerName, WaitStateName, states, transitions);
        }

        /// <summary>
        /// 追加パラメータと、その退避先を宣言に加える
        ///
        /// ブリッジ自身が宣言済みの名前と、追加パラメータ同士で重なる名前は宣言し直さない。
        /// AnimatorControllerは同名のパラメータを追加すると別名へ付け替えるため、
        /// 重ねて宣言するとDriverが書く相手と別のパラメータができてしまう。
        ///
        /// 退避先はExpression Parametersに載らないため、アニメーターだけのパラメータになる。
        /// 型を書き込み先と揃え、Copyで値が変換されないようにする。
        /// </summary>
        private static void DeclareExtraParameters(
            List<BridgeParameterPlan> parameters, IReadOnlyList<ExtraParameterTarget> extraParameters)
        {
            var declared = new HashSet<string>();
            foreach (var parameter in parameters)
            {
                declared.Add(parameter.Name);
            }

            foreach (var target in extraParameters)
            {
                if (declared.Add(target.Name))
                {
                    parameters.Add(new BridgeParameterPlan(target.Name, target.Type));
                }
            }

            foreach (var target in extraParameters)
            {
                if (target.Restore && declared.Add(target.StashName))
                {
                    parameters.Add(new BridgeParameterPlan(target.StashName, target.Type));
                }
            }
        }

        /// <summary>
        /// トリガーに合わせて、同期パラメータを書き込むレイヤー
        ///
        /// 既定のステートはDriverを持たないInitialにする。
        /// 解除時の値を書くステートを既定にすると、アバターを読み込むたびに解除時の値が書かれ、
        /// 装着者が保存していたトグルの状態を上書きしてしまう。
        /// Initialから出る遷移はトリガーが立ったときだけなので、解除時の値が書かれるのは
        /// フェイストラッキングを一度有効にしたあとに限られる。
        /// 復元では、退避値が未設定のまま書き戻されることも、同じ理由で起きない。
        ///
        /// 表情制御方式と同じく、周期的な書き直しは行わない。
        /// フェイストラッキング中でも、装着者がメニューから選び直した値を奪わないためである。
        ///
        /// Driverはlocal onlyで生成する。リモートでも書くと、書き込んだ値と届いた同期値が競合する。
        /// </summary>
        private static BridgeLayerPlan BuildExtraParametersLayer(
            BridgeSettings settings, IReadOnlyList<ExtraParameterTarget> extraParameters)
        {
            var states = new[]
            {
                new BridgeStatePlan(InitialStateName, DefaultClipLengthSeconds),
                new BridgeStatePlan(
                    EngagedStateName,
                    DefaultClipLengthSeconds,
                    driver: ExtraDriver(localOnly: true, EngageEntries(extraParameters, stash: true))),
                new BridgeStatePlan(
                    ReleasedStateName,
                    DefaultClipLengthSeconds,
                    driver: ExtraDriver(localOnly: true, ReleaseEntries(extraParameters))),
            };

            var transitions = new[]
            {
                new BridgeTransitionPlan(InitialStateName, EngagedStateName, new[] { TriggerOn(settings) }),
                new BridgeTransitionPlan(EngagedStateName, ReleasedStateName, new[] { TriggerOff(settings) }),
                new BridgeTransitionPlan(ReleasedStateName, EngagedStateName, new[] { TriggerOn(settings) }),
            };

            return new BridgeLayerPlan(ExtraParametersLayerName, InitialStateName, states, transitions);
        }

        /// <summary>
        /// トリガーに合わせて、同期しないパラメータを書き込むレイヤー
        ///
        /// 同期しないパラメータは値がほかの人へ届かないため、Driverを全クライアントで走らせ、
        /// 各クライアントが同じ値へ到達するようにする。
        ///
        /// トリガーが立っている間は、空クリップを1周するたびにRedriveを経由してReassertへ入り、
        /// 値を書き直す。後からjoinした人のクライアントでは、アバターのロード中に
        /// Driverの書き込みが失われることがあり、同期しない値は同期で補われないためである。
        /// 理由と構造はバイパス方式のレイヤーと同じである。
        ///
        /// 書き直すのは値だけで、退避はEngagedへ入ったときの一度に限る。
        /// Reassertでも退避すると、フェイストラッキング中の値を退避してしまう。
        /// 解除時の書き込みは書き直さない。解除後の値は、ほかのギミックやメニューの持ち物である。
        /// </summary>
        private static BridgeLayerPlan BuildUnsyncedExtraParametersLayer(
            BridgeSettings settings, IReadOnlyList<ExtraParameterTarget> extraParameters)
        {
            var states = new[]
            {
                new BridgeStatePlan(InitialStateName, DefaultClipLengthSeconds),
                new BridgeStatePlan(
                    EngagedStateName,
                    DefaultClipLengthSeconds,
                    driver: ExtraDriver(localOnly: false, EngageEntries(extraParameters, stash: true))),
                new BridgeStatePlan(
                    ReassertStateName,
                    DefaultClipLengthSeconds,
                    driver: ExtraDriver(localOnly: false, EngageEntries(extraParameters, stash: false))),
                new BridgeStatePlan(RedriveStateName, DefaultClipLengthSeconds),
                new BridgeStatePlan(
                    ReleasedStateName,
                    DefaultClipLengthSeconds,
                    driver: ExtraDriver(localOnly: false, ReleaseEntries(extraParameters))),
            };

            var transitions = new[]
            {
                new BridgeTransitionPlan(InitialStateName, EngagedStateName, new[] { TriggerOn(settings) }),

                // 解除の遷移を記載順の先に置き、書き直しのループより優先させる
                new BridgeTransitionPlan(EngagedStateName, ReleasedStateName, new[] { TriggerOff(settings) }),
                new BridgeTransitionPlan(
                    EngagedStateName,
                    RedriveStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f),
                new BridgeTransitionPlan(ReassertStateName, ReleasedStateName, new[] { TriggerOff(settings) }),
                new BridgeTransitionPlan(
                    ReassertStateName,
                    RedriveStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f),

                // Redriveはトリガーの現在値に合う側へ即座に抜ける中継である
                new BridgeTransitionPlan(RedriveStateName, ReassertStateName, new[] { TriggerOn(settings) }),
                new BridgeTransitionPlan(RedriveStateName, ReleasedStateName, new[] { TriggerOff(settings) }),

                new BridgeTransitionPlan(ReleasedStateName, EngagedStateName, new[] { TriggerOn(settings) }),
            };

            return new BridgeLayerPlan(UnsyncedExtraParametersLayerName, InitialStateName, states, transitions);
        }

        /// <summary>
        /// トリガーが立ったときに書くエントリ
        /// 退避するときは、すべての退避を書き込みより前に並べる。Driverはエントリを記載順に処理する
        /// </summary>
        private static List<BridgeDriverEntry> EngageEntries(
            IReadOnlyList<ExtraParameterTarget> extraParameters, bool stash)
        {
            var entries = new List<BridgeDriverEntry>();

            if (stash)
            {
                foreach (var target in extraParameters)
                {
                    if (target.Restore)
                    {
                        entries.Add(BridgeDriverEntry.Copy(target.Name, target.StashName));
                    }
                }
            }

            foreach (var target in extraParameters)
            {
                entries.Add(new BridgeDriverEntry(target.Name, target.EngagedValue));
            }

            return entries;
        }

        /// <summary>トリガーが下りたときに書くエントリ。復元は退避先から写し戻す</summary>
        private static List<BridgeDriverEntry> ReleaseEntries(IReadOnlyList<ExtraParameterTarget> extraParameters)
        {
            var entries = new List<BridgeDriverEntry>();

            foreach (var target in extraParameters)
            {
                if (target.Restore)
                {
                    entries.Add(BridgeDriverEntry.Copy(target.StashName, target.Name));
                }
                else if (target.ReleasedValue.HasValue)
                {
                    entries.Add(new BridgeDriverEntry(target.Name, target.ReleasedValue.Value));
                }
            }

            return entries;
        }

        /// <summary>書くものがなければDriverを付けない</summary>
        private static BridgeDriverPlan ExtraDriver(bool localOnly, List<BridgeDriverEntry> entries)
        {
            return entries.Count > 0 ? new BridgeDriverPlan(localOnly, entries) : null;
        }

        public static string ApplyStateName(bool eyeTracking, bool visemesEnabled)
        {
            return $"Apply_E{(eyeTracking ? 1 : 0)}_V{(visemesEnabled ? 1 : 0)}";
        }

        /// <summary>目のトラッキングとVisemesの全組み合わせ</summary>
        private static IEnumerable<(bool eye, bool visemes)> TrackingCombinations()
        {
            yield return (false, false);
            yield return (false, true);
            yield return (true, false);
            yield return (true, true);
        }

        private static BridgeDriverPlan BypassDriver(float value)
        {
            return new BridgeDriverPlan(
                localOnly: false,
                entries: new[] { new BridgeDriverEntry(BridgeParameterNames.ForceBypassEnable, value) });
        }

        /// <summary>表情ロックとまばたき停止を起こし、指定の表情へ切り替えるDriver</summary>
        private static BridgeDriverPlan EngageDriver(FaceEmoParameterNames faceEmo, int emoteIndex)
        {
            return new BridgeDriverPlan(
                localOnly: true,
                entries: new[]
                {
                    new BridgeDriverEntry(faceEmo.EmoteLockEnable, 1f),
                    new BridgeDriverEntry(faceEmo.ForceBlinkDisable, 1f),
                    new BridgeDriverEntry(faceEmo.Emote, emoteIndex),
                });
        }

        /// <summary>
        /// 表情ロックとまばたき停止を元へ戻すDriver
        ///
        /// 表情番号は戻さない。戻す先を覚える手立てがなく、ロックが外れれば
        /// FaceEmoがジェスチャーから決め直すためである。
        /// </summary>
        private static BridgeDriverPlan ReleaseDriver(FaceEmoParameterNames faceEmo)
        {
            return new BridgeDriverPlan(
                localOnly: true,
                entries: new[]
                {
                    new BridgeDriverEntry(faceEmo.EmoteLockEnable, 0f),
                    new BridgeDriverEntry(faceEmo.ForceBlinkDisable, 0f),
                });
        }

        public static BridgeConditionPlan TriggerOn(BridgeSettings settings)
        {
            return settings.BypassTrigger == BypassTrigger.LipTrackingOnly
                ? new BridgeConditionPlan(
                    BridgeParameterNames.LipTrackingActive, BridgeConditionMode.Greater, FloatTriggerThreshold)
                : new BridgeConditionPlan(
                    BridgeParameterNames.FacialExpressionsDisabled, BridgeConditionMode.If);
        }

        public static BridgeConditionPlan TriggerOff(BridgeSettings settings)
        {
            return settings.BypassTrigger == BypassTrigger.LipTrackingOnly
                ? new BridgeConditionPlan(
                    BridgeParameterNames.LipTrackingActive, BridgeConditionMode.Less, FloatTriggerThreshold)
                : new BridgeConditionPlan(
                    BridgeParameterNames.FacialExpressionsDisabled, BridgeConditionMode.IfNot);
        }

        private static BridgeConditionPlan EyeCondition(bool active)
        {
            return new BridgeConditionPlan(
                BridgeParameterNames.EyeTrackingActive,
                active ? BridgeConditionMode.Greater : BridgeConditionMode.Less,
                FloatTriggerThreshold);
        }

        private static BridgeConditionPlan VisemesCondition(bool enabled)
        {
            // Jerry自身のMouth切替はメニューの同期パラメータをそのまま条件に持つため、
            // 同じ値を読めばApplyステートの選択がJerryの適用と一致する
            return new BridgeConditionPlan(
                BridgeParameterNames.VisemesEnable,
                enabled ? BridgeConditionMode.If : BridgeConditionMode.IfNot);
        }
    }
}
