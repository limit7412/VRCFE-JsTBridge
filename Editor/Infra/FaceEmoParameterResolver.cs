using System.Collections.Generic;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// アバターに載っているMA Parametersから、FaceEmoのパラメータのリネームを読み取る
    /// </summary>
    internal static class FaceEmoParameterResolver
    {
        /// <summary>
        /// MA Parametersの登録内容を読み、FaceEmoのパラメータ名を解決する
        /// </summary>
        public static FaceEmoParameterNames Resolve(GameObject avatarRoot)
        {
            return FaceEmoParameterNames.Resolve(CollectParameterRemaps(avatarRoot));
        }

        /// <summary>
        /// コンポーネントごとに、元の名前からリネーム後の名前への対応を作る
        /// </summary>
        /// <remarks>
        /// 非アクティブなオブジェクトも対象に含める。環境検出と同じく、
        /// 導入直後にオフのまま置かれたプレハブを見落とさないためである。
        /// </remarks>
        internal static IReadOnlyList<IReadOnlyDictionary<string, string>> CollectParameterRemaps(
            GameObject avatarRoot)
        {
            var remaps = new List<IReadOnlyDictionary<string, string>>();
            if (avatarRoot == null)
            {
                return remaps;
            }

            foreach (var component in avatarRoot.GetComponentsInChildren<ModularAvatarParameters>(true))
            {
                if (component == null || component.parameters == null)
                {
                    continue;
                }

                var remap = new Dictionary<string, string>();

                foreach (var config in component.parameters)
                {
                    if (string.IsNullOrEmpty(config.nameOrPrefix))
                    {
                        continue;
                    }

                    // 前置き一致の登録は名前そのものの対応を持たない。
                    // 内部パラメータはMAがビルド時に固有の名前へ変えるため、外からは接続できない。
                    // どちらも読み飛ばし、解決できなかったものとして扱う
                    if (config.isPrefix || config.internalParameter)
                    {
                        continue;
                    }

                    remap[config.nameOrPrefix] =
                        string.IsNullOrEmpty(config.remapTo) ? config.nameOrPrefix : config.remapTo;
                }

                if (remap.Count > 0)
                {
                    remaps.Add(remap);
                }
            }

            return remaps;
        }
    }
}
