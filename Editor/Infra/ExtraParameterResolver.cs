using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// 追加パラメータの設定から、FXで使われる名前と書き込む値を解決する
    ///
    /// 名前の解決にはNDMFのParameterInfoを使う。MAもメニューアイテムのリネームに同じ仕組みを使っており、
    /// 内部パラメータに振る名前はビルドコンテキストごとに記憶されるため、ここで引いた名前と
    /// MAがあとで振る名前は一致する。
    /// </summary>
    internal static class ExtraParameterResolver
    {
        internal sealed class Result
        {
            public Result(IReadOnlyList<ExtraParameterTarget> targets, IReadOnlyList<Issue> issues)
            {
                Targets = targets;
                Issues = issues;
            }

            public IReadOnlyList<ExtraParameterTarget> Targets { get; }

            public IReadOnlyList<Issue> Issues { get; }
        }

        /// <summary>
        /// 解決できなかった項目
        /// 項目の番号は1から数え、インスペクタのリストの並びと一致させる
        /// </summary>
        internal sealed class Issue
        {
            public Issue(string messageKey, int entryNumber, Object reference = null)
            {
                MessageKey = messageKey;
                EntryNumber = entryNumber;
                Reference = reference;
            }

            public string MessageKey { get; }

            public int EntryNumber { get; }

            public Object Reference { get; }
        }

        public static Result Resolve(
            BuildContext context, GameObject avatarRoot, IReadOnlyList<ExtraParameterEntry> entries)
        {
            var targets = new List<ExtraParameterTarget>();
            var issues = new List<Issue>();

            if (entries == null || entries.Count == 0 || avatarRoot == null)
            {
                return new Result(targets, issues);
            }

            var parameterInfo = ParameterInfo.ForContext(context);

            // 宣言とメニューアイテムの一覧は、自動の値を求める項目があるときに初めて使う
            Dictionary<string, DeclaredParameter> declared = null;
            List<(ModularAvatarMenuItem item, string name)> menuItems = null;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var number = i + 1;

                if (entry == null)
                {
                    continue;
                }

                if (entry.source == ExtraParameterSource.ParameterName)
                {
                    var name = entry.parameterName?.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        issues.Add(new Issue("warning.extra_parameter.empty_name", number));
                        continue;
                    }

                    targets.Add(ExtraParameterTarget.FromDirect(
                        name, entry.parameterType, entry.engagedValue, entry.releaseMode, entry.releasedValue));
                    continue;
                }

                var menuItem = entry.menuItem;
                if (menuItem == null)
                {
                    issues.Add(new Issue("warning.extra_parameter.missing_menu_item", number));
                    continue;
                }

                if (!menuItem.transform.IsChildOf(avatarRoot.transform))
                {
                    issues.Add(new Issue("warning.extra_parameter.outside_avatar", number, menuItem));
                    continue;
                }

                var control = menuItem.Control;
                if (control == null
                    || (control.type != VRCExpressionsMenu.Control.ControlType.Toggle
                        && control.type != VRCExpressionsMenu.Control.ControlType.Button))
                {
                    issues.Add(new Issue("warning.extra_parameter.unsupported_control", number, menuItem));
                    continue;
                }

                // パラメータ名が空のメニューアイテムには、MAがビルド中に固有の名前を振る。
                // 振られる名前は通し番号を含み、ここからは求められない
                var rawName = control.parameter?.name;
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    issues.Add(new Issue("warning.extra_parameter.unnamed_parameter", number, menuItem));
                    continue;
                }

                var effectiveName = ResolveName(parameterInfo, menuItem.gameObject, rawName);

                float value;
                if (menuItem.automaticValue)
                {
                    declared = declared ?? CollectDeclaredParameters(parameterInfo, avatarRoot);
                    menuItems = menuItems ?? CollectMenuItems(parameterInfo, avatarRoot);

                    var itemCount = 0;
                    foreach (var (_, name) in menuItems)
                    {
                        if (name == effectiveName)
                        {
                            itemCount++;
                        }
                    }

                    var automatic = MenuItemToggleValue.ResolveAutomatic(
                        menuItem.isDefault,
                        itemCount,
                        declared.TryGetValue(effectiveName, out var declaration)
                            ? declaration.DefaultValue
                            : (float?)null);

                    if (!automatic.HasValue)
                    {
                        issues.Add(new Issue("warning.extra_parameter.automatic_value", number, menuItem));
                        continue;
                    }

                    value = automatic.Value;
                }
                else
                {
                    value = control.value;
                }

                // 宣言済みならその型に合わせる。未宣言ならMAがトグル値から型を決めるので、同じ規則で求める
                declared = declared ?? CollectDeclaredParameters(parameterInfo, avatarRoot);
                var type = declared.TryGetValue(effectiveName, out var declaredParameter)
                    ? declaredParameter.Type
                    : MenuItemToggleValue.TypeOf(value);

                targets.Add(ExtraParameterTarget.FromMenuItem(
                    effectiveName, type, value, entry.menuItemState, entry.releaseMode));
            }

            return new Result(targets, issues);
        }

        /// <summary>
        /// 指定したオブジェクトの位置で有効なリネームを当てて、FXで使われる名前を求める
        /// </summary>
        private static string ResolveName(ParameterInfo parameterInfo, GameObject at, string name)
        {
            var remaps = parameterInfo.GetParameterRemappingsAt(at);
            return remaps.TryGetValue((ParameterNamespace.Animator, name), out var mapping)
                ? mapping.ParameterName
                : name;
        }

        /// <summary>
        /// アバター内のメニューアイテムを、リネーム後のパラメータ名とともに集める
        /// 非アクティブなオブジェクトも含める。MAの値の割り当ても非アクティブなものを数えるためである
        /// </summary>
        private static List<(ModularAvatarMenuItem item, string name)> CollectMenuItems(
            ParameterInfo parameterInfo, GameObject avatarRoot)
        {
            var items = new List<(ModularAvatarMenuItem item, string name)>();

            foreach (var item in avatarRoot.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                var rawName = item.Control?.parameter?.name;
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    continue;
                }

                items.Add((item, ResolveName(parameterInfo, item.gameObject, rawName)));
            }

            return items;
        }

        private readonly struct DeclaredParameter
        {
            public DeclaredParameter(BridgeParameterType type, float defaultValue)
            {
                Type = type;
                DefaultValue = defaultValue;
            }

            public BridgeParameterType Type { get; }

            public float DefaultValue { get; }
        }

        /// <summary>
        /// Expression ParametersとMA Parametersで宣言済みのパラメータを集める
        ///
        /// MAは値を割り当てる時点で、MA Parametersの同期パラメータをExpression Parametersへ
        /// 追加し終えている。そのため両方を宣言済みとして扱う。
        /// 同期しない登録はExpression Parametersへ追加されないので含めない。
        /// </summary>
        private static Dictionary<string, DeclaredParameter> CollectDeclaredParameters(
            ParameterInfo parameterInfo, GameObject avatarRoot)
        {
            var declared = new Dictionary<string, DeclaredParameter>();

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var expressionParameters = descriptor != null ? descriptor.expressionParameters : null;
            if (expressionParameters != null && expressionParameters.parameters != null)
            {
                foreach (var parameter in expressionParameters.parameters)
                {
                    if (parameter == null || string.IsNullOrEmpty(parameter.name)
                        || declared.ContainsKey(parameter.name))
                    {
                        continue;
                    }

                    declared[parameter.name] = new DeclaredParameter(
                        ToBridgeType(parameter.valueType), parameter.defaultValue);
                }
            }

            foreach (var component in avatarRoot.GetComponentsInChildren<ModularAvatarParameters>(true))
            {
                if (component == null || component.parameters == null)
                {
                    continue;
                }

                // 自身の登録によるリネームも含めて引く。内部パラメータの名前もここで決まる
                var remaps = parameterInfo.GetParameterRemappingsAt(component, true);

                foreach (var config in component.parameters)
                {
                    if (config.isPrefix || string.IsNullOrEmpty(config.nameOrPrefix)
                        || config.syncType == ParameterSyncType.NotSynced)
                    {
                        continue;
                    }

                    var name = remaps.TryGetValue((ParameterNamespace.Animator, config.nameOrPrefix), out var mapping)
                        ? mapping.ParameterName
                        : config.nameOrPrefix;

                    if (declared.ContainsKey(name))
                    {
                        continue;
                    }

                    declared[name] = new DeclaredParameter(ToBridgeType(config.syncType), config.defaultValue);
                }
            }

            return declared;
        }

        private static BridgeParameterType ToBridgeType(VRCExpressionParameters.ValueType type)
        {
            switch (type)
            {
                case VRCExpressionParameters.ValueType.Int:
                    return BridgeParameterType.Int;
                case VRCExpressionParameters.ValueType.Float:
                    return BridgeParameterType.Float;
                default:
                    return BridgeParameterType.Bool;
            }
        }

        private static BridgeParameterType ToBridgeType(ParameterSyncType type)
        {
            switch (type)
            {
                case ParameterSyncType.Int:
                    return BridgeParameterType.Int;
                case ParameterSyncType.Float:
                    return BridgeParameterType.Float;
                default:
                    return BridgeParameterType.Bool;
            }
        }
    }
}
