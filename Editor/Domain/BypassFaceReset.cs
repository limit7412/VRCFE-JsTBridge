using System.Collections.Generic;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// バイパス中に、FaceEmoが最後に書いた表情が顔へ残るかの判定
    ///
    /// FaceEmoのバイパス用ステートは、ブレンドシェイプを書かない空のクリップを再生する。
    /// Write Defaultsが有効なら、そのステートがブレンドシェイプを既定値へ戻す。
    /// 無効だと戻すものがいないため、バイパスが成立した瞬間の値がそのまま残る。
    /// まばたきの途中やジェスチャー表情の最中にトラッキングを有効にすると、
    /// その顔がトラッキングの上に重なり、無効にするまで戻らない。
    /// </summary>
    internal static class BypassFaceReset
    {
        /// <summary>
        /// マージ後のFaceEmoに、Write Defaultsが無効なステートが残るか
        /// </summary>
        /// <param name="avatarWriteDefaults">
        /// アバター自身のFXのWrite Defaults。有効と無効が混ざっているか、ステートが無ければ null
        /// </param>
        /// <remarks>
        /// Modular Avatarは、Merge Animatorの「アバターのWrite Defaults設定に合わせる」が有効で、
        /// アバターのFXが有効か無効のどちらかに揃っているとき、マージするステートをそちらへ書き換える。
        /// 揃っていなければコントローラの設定がそのまま残る。
        /// </remarks>
        public static bool LeavesFace(
            bool? avatarWriteDefaults, IEnumerable<FaceEmoWriteDefaults> faceEmoControllers)
        {
            if (faceEmoControllers == null)
            {
                return false;
            }

            foreach (var controller in faceEmoControllers)
            {
                var overridden = controller.MatchAvatarWriteDefaults && avatarWriteDefaults.HasValue;
                var writeDefaultsOff = overridden
                    ? !avatarWriteDefaults.Value
                    : controller.HasWriteDefaultsOffState;

                if (writeDefaultsOff)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// FaceEmoのコントローラ一件分の、Write Defaultsにかかわる設定
    /// </summary>
    internal readonly struct FaceEmoWriteDefaults
    {
        public FaceEmoWriteDefaults(bool matchAvatarWriteDefaults, bool hasWriteDefaultsOffState)
        {
            MatchAvatarWriteDefaults = matchAvatarWriteDefaults;
            HasWriteDefaultsOffState = hasWriteDefaultsOffState;
        }

        /// <summary>Merge Animatorの「アバターのWrite Defaults設定に合わせる」</summary>
        public bool MatchAvatarWriteDefaults { get; }

        /// <summary>コントローラ自身に、Write Defaultsが無効なステートがあるか</summary>
        public bool HasWriteDefaultsOffState { get; }
    }
}
