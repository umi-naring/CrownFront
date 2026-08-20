using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaLocaleDefault283Routine()
        {
            yield return null;
            var failures = new List<string>();
            if (GameLocalization.DefaultForSystemLanguage(SystemLanguage.Korean) != GameLanguage.Korean)
                failures.Add("korean-default");
            var nonKorean = new[]
            {
                SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.ChineseSimplified,
                SystemLanguage.ChineseTraditional, SystemLanguage.Spanish, SystemLanguage.Unknown
            };
            foreach (var language in nonKorean)
                if (GameLocalization.DefaultForSystemLanguage(language) != GameLanguage.English)
                    failures.Add($"non-korean-{language}");

            var hadPreference = PlayerPrefs.HasKey(GameLocalization.PreferenceKey);
            var savedPreference = PlayerPrefs.GetInt(GameLocalization.PreferenceKey, 0);
            try
            {
                PlayerPrefs.SetInt(GameLocalization.PreferenceKey, (int)GameLanguage.English);
                if (GameLocalization.LoadInitialLanguage() != GameLanguage.English)
                    failures.Add("saved-choice-overwritten");
            }
            finally
            {
                if (hadPreference)
                    PlayerPrefs.SetInt(GameLocalization.PreferenceKey, savedPreference);
                else
                    PlayerPrefs.DeleteKey(GameLocalization.PreferenceKey);
                PlayerPrefs.Save();
            }

            var passed = failures.Count == 0;
            Debug.Log($"QA_LOCALE_DEFAULT_283 passed={passed} korean=Korean nonKorean=English " +
                      $"savedChoice=True fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 123);
        }
    }
}
