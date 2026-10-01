using System.Security.Cryptography;
using System.Text;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// パラメータ名が空のMA Menu Itemに、Modular Avatarがビルド中に振る名前のなぞり
    ///
    /// MA 1.12以降のParameterAssignerPassは、接頭辞とオブジェクト名のあとに、
    /// アバタールートからのパスのSHA-256の先頭6バイトを16進で付ける。
    /// パスから決まるため、ブリッジがMAより先に動く段階でも同じ名前を求められる。
    /// 1.11以前は通し番号を使っており再現できないので、vpmDependenciesの下限を1.12.0にしている。
    /// </summary>
    public static class MenuItemAutoParameterName
    {
        /// <summary>MAのParameterAssignerPass.AUTOMATIC_PARAMETER_PREFIXと同じ値</summary>
        public const string Prefix = "__MA/AutoParam/";

        /// <summary>
        /// オブジェクト名とアバタールートからのパスから、MAが振る名前を組み立てる
        /// </summary>
        /// <param name="objectName">メニューアイテムのGameObjectの名前</param>
        /// <param name="avatarRootPath">アバタールートからの相対パス。ルート自身なら空文字</param>
        public static string Build(string objectName, string avatarRootPath)
        {
            return Prefix + objectName + "$" + HashOf(avatarRootPath ?? string.Empty);
        }

        private static string HashOf(string path)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(path));
                var builder = new StringBuilder(12);
                for (var i = 0; i < 6; i++)
                {
                    builder.AppendFormat("{0:x2}", bytes[i]);
                }

                return builder.ToString();
            }
        }
    }
}
