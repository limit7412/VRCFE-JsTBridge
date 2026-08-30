using System;
using System.IO;
using UnityEditor.Compilation;
using UnityEngine;

namespace FEJsTBridge.Infra
{
    /// <summary>
    /// 手元に入っているパッケージの版を、asmdefの位置を起点に読む。
    ///
    /// Assets/配下・Packages/配下のどちらに置かれても解決できるよう、
    /// Localization.GetLocalizationRootと同じくasmdefから辿る。
    /// 結果はドメインリロードまで変わらないため、一度求めたら使い回す
    /// </summary>
    internal static class PackageLocation
    {
        private const string EditorAssemblyName = "FEJsTBridge.Editor";
        private const string EditorDirectoryName = "Editor";
        private const string ManifestFileName = "package.json";

        private static bool _resolved;
        private static string _version;

        /// <summary>package.jsonが持つ手元の版。読めなければnull</summary>
        public static string Version
        {
            get
            {
                Resolve();
                return _version;
            }
        }

        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(EditorAssemblyName);
            if (!TryResolvePackageRoot(asmdefPath, out var packageRoot))
            {
                return;
            }

            _version = ReadVersion(packageRoot);
        }

        /// <summary>
        /// 「ルート/Editor/なにか.asmdef」の形を前提に、パッケージのルートを取り出す。
        ///
        /// 形が合わないパスでは偽を返す。
        /// ルートが分からなければpackage.jsonも読めず、手元の版が確かめられない
        /// </summary>
        internal static bool TryResolvePackageRoot(string editorAsmdefPath, out string packageRoot)
        {
            packageRoot = null;

            if (string.IsNullOrWhiteSpace(editorAsmdefPath))
            {
                return false;
            }

            // Unityのパスは`/`区切りだが、環境によっては`\`が混ざったものが返る
            var segments = editorAsmdefPath.Trim().Replace('\\', '/').Split('/');

            // ルート・Editor・asmdefの3要素が最低限必要
            if (segments.Length < 3)
            {
                return false;
            }

            if (!string.Equals(segments[segments.Length - 2], EditorDirectoryName, StringComparison.Ordinal))
            {
                return false;
            }

            packageRoot = string.Join("/", segments, 0, segments.Length - 2);
            return true;
        }

        private static string ReadVersion(string packageRoot)
        {
            // リリース時にrelease.ymlがタグでversionを書き換えてからzipへ入れるため、
            // 配布物のpackage.jsonは常にそのリリースの版を持つ
            try
            {
                var manifestPath = Path.Combine(packageRoot, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    return null;
                }

                return TryParseVersion(File.ReadAllText(manifestPath), out var version) ? version : null;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException)
            {
                // 版が読めなくても更新の通知が出なくなるだけで、ブリッジの生成には影響しない
                return null;
            }
        }

        /// <summary>package.jsonのversionを取り出す。読めない形なら偽を返す</summary>
        internal static bool TryParseVersion(string manifestJson, out string version)
        {
            version = null;

            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                return false;
            }

            try
            {
                var manifest = JsonUtility.FromJson<PackageManifest>(manifestJson);
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.version))
                {
                    return false;
                }

                version = manifest.version.Trim();
                return true;
            }
            catch (ArgumentException)
            {
                // JsonUtilityはJSONとして読めない文字列でArgumentExceptionを投げる
                return false;
            }
        }

        [Serializable]
        private class PackageManifest
        {
            // JsonUtilityはフィールド名でJSONのキーと対応づけるため、package.jsonの綴りに合わせる
            public string version;
        }
    }
}
