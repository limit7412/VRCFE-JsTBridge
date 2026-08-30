using UnityEditor;
using UnityEngine;
using FEJsTBridge.Infra;
using static FEJsTBridge.Localization;

namespace FEJsTBridge.Presentation
{
    /// <summary>
    /// 更新の確認についての問いかけと、新しい版が出ているときの案内をインスペクタの先頭へ描く。
    ///
    /// 案内はインストール形態で分けない。
    /// このパッケージの配布はzip1種類で、VCC/ALCOM経由でも手動の「Add from Archive」でも
    /// `Packages/`配下へ入るため、置き場所から更新の手段は判別できない。
    /// 版を差し替える主体はVCC/ALCOMであり、こちらでファイルを置き換えると
    /// vpm-manifest.jsonの記録と実態がずれるので、案内するのは入手先までにとどめる
    /// </summary>
    internal static class UpdateNotice
    {
        public static void Draw()
        {
            switch (UpdateCheck.Preference)
            {
                case UpdateCheckPreference.Unset:
                    DrawConsent();
                    break;
                case UpdateCheckPreference.Enabled:
                    UpdateCheck.PollIfDue();
                    DrawAvailableUpdate();
                    break;
            }
        }

        private static void DrawConsent()
        {
            EditorGUILayout.HelpBox(S("update.consent.description"), MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(S("update.consent.enable")))
            {
                UpdateCheck.Preference = UpdateCheckPreference.Enabled;
            }

            if (GUILayout.Button(S("update.consent.disable")))
            {
                UpdateCheck.Preference = UpdateCheckPreference.Disabled;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private static void DrawAvailableUpdate()
        {
            var tag = UpdateCheck.PendingUpdateTag;
            if (tag == null)
            {
                return;
            }

            EditorGUILayout.HelpBox(S("update.available", tag), MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(S("update.open_releases")))
            {
                Application.OpenURL(UpdateCheck.ReleasesPageUrl);
            }

            if (GUILayout.Button(S("update.dismiss")))
            {
                UpdateCheck.Dismiss(tag);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }
    }

    /// <summary>
    /// 更新確認の入切をPreferencesからも変えられるようにする。
    ///
    /// インスペクタの問いかけで「確認しない」を選ぶと、以後その問いかけは出ない。
    /// 選び直す場所がここになる
    /// </summary>
    internal static class UpdateCheckSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Preferences/Kx VRC FE-JsT Bridge", SettingsScope.User)
            {
                guiHandler = _ =>
                {
                    var enabled = UpdateCheck.Preference == UpdateCheckPreference.Enabled;
                    var changed = EditorGUILayout.Toggle(S("update.settings.enabled"), enabled);
                    if (changed != enabled)
                    {
                        UpdateCheck.Preference = changed
                            ? UpdateCheckPreference.Enabled
                            : UpdateCheckPreference.Disabled;
                    }

                    EditorGUILayout.HelpBox(S("update.settings.description"), MessageType.None);
                },
            };
        }
    }
}
