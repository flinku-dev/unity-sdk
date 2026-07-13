using UnityEngine;

namespace Flinku
{
    public sealed class PlayerPrefsKeyValueStore : IFlinkuKeyValueStore
    {
        public bool GetBool(string key, bool defaultValue = false) =>
            PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;

        public void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public string GetString(string key, string defaultValue = null)
        {
            if (!PlayerPrefs.HasKey(key)) return defaultValue;
            return PlayerPrefs.GetString(key, defaultValue ?? "");
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void RemoveKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        public System.Collections.Generic.IEnumerable<string> AllKeys()
        {
            // PlayerPrefs has no key enumeration; scan common Flinku keys.
            var keys = new System.Collections.Generic.List<string>();
            foreach (var key in new[]
                     {
                         FlinkuReferralCore.UserIdKey,
                         FlinkuReferralCore.ReferralProjectIdKey,
                         "flinku_matched",
                         FlinkuReferralCore.MatchResultKey,
                     })
            {
                if (PlayerPrefs.HasKey(key)) keys.Add(key);
            }

            // Pending keys use project id suffix — stored separately in referral project id when possible.
            var projectId = GetString(FlinkuReferralCore.ReferralProjectIdKey);
            if (!string.IsNullOrEmpty(projectId))
            {
                keys.Add(FlinkuReferralCore.PendingReferralKey(projectId));
            }

            return keys;
        }
    }
}
