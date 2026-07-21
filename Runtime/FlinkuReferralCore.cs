using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Flinku
{
    public interface IFlinkuReferralNetwork
    {
        void PostReferral(
            string apiBaseUrl,
            string path,
            Dictionary<string, object> body,
            string apiKey,
            Action<bool> onComplete);
    }

    public sealed class PendingReferralPayload
    {
        [JsonProperty("referrerId")]
        public string ReferrerId;

        [JsonProperty("referrerLabel")]
        public string ReferrerLabel;

        [JsonProperty("linkId")]
        public string LinkId;

        [JsonProperty("matchedAt")]
        public double MatchedAt;
    }

    public static class FlinkuReferralCore
    {
        public const string UserIdKey = "flinku_user_id";
        public const string ReferralProjectIdKey = "flinku_referral_project_id";
        public const string PendingReferralKeyPrefix = "flinku_pending_referral_";
        public const string PendingReferralIndexKey = "flinku_pending_referral_index";
        public const string MatchResultKey = "flinku_match_result";
        public const double PendingReferralTtlSeconds = 30 * 24 * 60 * 60;

        public static IFlinkuKeyValueStore Store { get; set; } = new InMemoryKeyValueStore();
        public static IFlinkuReferralNetwork Network { get; set; }
        public static Action<string> LogSink { get; set; }

        private static bool _referralApiKeyWarningShown;

        public static void ResetForTesting()
        {
            Store = new InMemoryKeyValueStore();
            Network = null;
            LogSink = null;
            _referralApiKeyWarningShown = false;
        }

        public static string ReferralTrackedKey(string projectId, string userId) =>
            $"referral_tracked_{projectId}_{userId}";

        public static string PendingReferralKey(string projectId) =>
            $"{PendingReferralKeyPrefix}{projectId}";

        private static void Log(string message)
        {
            LogSink?.Invoke(message);
        }

        public static void WarnMissingReferralApiKeyOnce(string apiKey)
        {
            if (!string.IsNullOrWhiteSpace(apiKey) || _referralApiKeyWarningShown) return;
            _referralApiKeyWarningShown = true;
            Log("[Flinku] Referral tracking skipped: no apiKey configured. Pass apiKey: 'flk_pk_...' to Flinku.configure().");
        }

        public static bool HasReferralApiKey(string apiKey) =>
            !string.IsNullOrWhiteSpace(apiKey);

        public static void PersistPendingReferralIfNeeded(FlinkuLink link)
        {
            if (link == null) return;
            var projectId = link.ProjectId?.Trim();
            if (string.IsNullOrEmpty(projectId)) return;
            if (link.Params == null) return;

            string referrerId = null;
            if (link.Params.TryGetValue("referrerId", out var rid) && !string.IsNullOrWhiteSpace(rid))
                referrerId = rid.Trim();
            else if (link.Params.TryGetValue("referrer_id", out var ridSnake) && !string.IsNullOrWhiteSpace(ridSnake))
                referrerId = ridSnake.Trim();
            if (string.IsNullOrEmpty(referrerId)) return;

            string referrerLabel = null;
            if (link.Params.TryGetValue("referrerLabel", out var label) && !string.IsNullOrWhiteSpace(label))
                referrerLabel = label.Trim();
            else if (link.Params.TryGetValue("referrer_label", out var labelSnake) && !string.IsNullOrWhiteSpace(labelSnake))
                referrerLabel = labelSnake.Trim();

            var linkId = !string.IsNullOrWhiteSpace(link.LinkId) ? link.LinkId.Trim()
                : !string.IsNullOrWhiteSpace(link.Id) ? link.Id.Trim()
                : !string.IsNullOrWhiteSpace(link.Slug) ? link.Slug.Trim() : null;

            var payload = new PendingReferralPayload
            {
                ReferrerId = referrerId,
                MatchedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            if (!string.IsNullOrEmpty(referrerLabel)) payload.ReferrerLabel = referrerLabel;
            if (!string.IsNullOrEmpty(linkId)) payload.LinkId = linkId;

            Store.SetString(PendingReferralKey(projectId), JsonConvert.SerializeObject(payload));
            Store.SetString(ReferralProjectIdKey, projectId);
            AddPendingIndex(projectId);
        }

        private static void AddPendingIndex(string projectId)
        {
            var ids = GetPendingIndex();
            if (!ids.Contains(projectId)) ids.Add(projectId);
            Store.SetString(PendingReferralIndexKey, string.Join(",", ids));
        }

        private static System.Collections.Generic.List<string> GetPendingIndex()
        {
            var raw = Store.GetString(PendingReferralIndexKey);
            if (string.IsNullOrEmpty(raw))
            {
                var list = new System.Collections.Generic.List<string>();
                var pid = Store.GetString(ReferralProjectIdKey);
                if (!string.IsNullOrEmpty(pid)) list.Add(pid);
                return list;
            }
            return new System.Collections.Generic.List<string>(
                raw.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        private static void RemovePendingIndex(string projectId)
        {
            var ids = GetPendingIndex();
            ids.RemoveAll(id => id == projectId);
            if (ids.Count == 0) Store.RemoveKey(PendingReferralIndexKey);
            else Store.SetString(PendingReferralIndexKey, string.Join(",", ids));
        }

        public static (string projectId, PendingReferralPayload payload)? LoadPendingReferral()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var projectId in GetPendingIndex())
            {
                var key = PendingReferralKey(projectId);
                var raw = Store.GetString(key);
                if (string.IsNullOrEmpty(raw)) continue;

                PendingReferralPayload json;
                try
                {
                    json = JsonConvert.DeserializeObject<PendingReferralPayload>(raw);
                }
                catch
                {
                    continue;
                }

                if (json == null || json.MatchedAt <= 0)
                {
                    Store.RemoveKey(key);
                    RemovePendingIndex(projectId);
                    continue;
                }

                if (now - json.MatchedAt > PendingReferralTtlSeconds)
                {
                    Store.RemoveKey(key);
                    RemovePendingIndex(projectId);
                    continue;
                }

                var referrerId = json.ReferrerId?.Trim();
                if (string.IsNullOrEmpty(referrerId))
                {
                    Store.RemoveKey(key);
                    RemovePendingIndex(projectId);
                    continue;
                }

                return (projectId, json);
            }

            return null;
        }

        public static void ClearPendingReferral(string projectId)
        {
            Store.RemoveKey(PendingReferralKey(projectId));
            RemovePendingIndex(projectId);
        }

        public static void TrackReferralInBackground(
            string userId,
            string apiKey,
            string apiBaseUrl,
            int timeoutMs)
        {
            if (!HasReferralApiKey(apiKey)) return;
            var pending = LoadPendingReferral();
            if (pending == null) return;

            var projectId = pending.Value.projectId;
            var json = pending.Value.payload;
            var trackedKey = ReferralTrackedKey(projectId, userId);

            if (Store.GetBool(trackedKey))
            {
                ClearPendingReferral(projectId);
                return;
            }

            var referrerId = json.ReferrerId?.Trim();
            if (string.IsNullOrEmpty(referrerId))
            {
                ClearPendingReferral(projectId);
                return;
            }

            var body = new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["referrerId"] = referrerId,
                ["newUserId"] = userId,
            };
            if (!string.IsNullOrWhiteSpace(json.ReferrerLabel))
                body["referrerLabel"] = json.ReferrerLabel.Trim();
            if (!string.IsNullOrWhiteSpace(json.LinkId))
                body["linkId"] = json.LinkId.Trim();

            Store.SetString(ReferralProjectIdKey, projectId);

            var network = Network;
            if (network == null)
            {
                Log("[Flinku] referral network not configured");
                return;
            }

            network.PostReferral(apiBaseUrl, "/api/referrals/track", body, apiKey, success =>
            {
                if (!success) return;
                Store.SetBool(trackedKey, true);
                ClearPendingReferral(projectId);
            });
        }

        public static void ClearAllReferralState()
        {
            var store = Store;
            var userId = store.GetString(UserIdKey)?.Trim();
            var projectIds = GetPendingIndex().ToList();
            var referralProjectId = store.GetString(ReferralProjectIdKey)?.Trim();
            if (!string.IsNullOrEmpty(referralProjectId) && !projectIds.Contains(referralProjectId))
                projectIds.Add(referralProjectId);

            store.RemoveKey(UserIdKey);
            store.RemoveKey(ReferralProjectIdKey);
            store.RemoveKey(MatchResultKey);

            foreach (var projectId in projectIds)
            {
                store.RemoveKey(PendingReferralKey(projectId));
                if (!string.IsNullOrEmpty(userId))
                    store.RemoveKey(ReferralTrackedKey(projectId, userId));
            }

            store.RemoveKey(PendingReferralIndexKey);

            foreach (var key in store.AllKeys())
            {
                if (key.StartsWith(PendingReferralKeyPrefix, StringComparison.Ordinal) ||
                    key.StartsWith("referral_tracked_", StringComparison.Ordinal))
                {
                    store.RemoveKey(key);
                }
            }
        }

        public static void QualifyReferralInBackground(
            string eventName,
            string apiKey,
            string apiBaseUrl,
            int timeoutMs)
        {
            if (!HasReferralApiKey(apiKey)) return;
            var userId = Store.GetString(UserIdKey)?.Trim();
            if (string.IsNullOrEmpty(userId)) return;

            var projectId = Store.GetString(ReferralProjectIdKey)?.Trim();
            if (string.IsNullOrEmpty(projectId))
                projectId = LoadPendingReferral()?.projectId;

            if (string.IsNullOrEmpty(projectId))
            {
                var stored = Store.GetString(MatchResultKey);
                if (!string.IsNullOrEmpty(stored))
                {
                    try
                    {
                        var link = JsonConvert.DeserializeObject<FlinkuLink>(stored);
                        projectId = link?.ProjectId?.Trim();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            if (string.IsNullOrEmpty(projectId)) return;

            var body = new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["newUserId"] = userId,
            };
            if (!string.IsNullOrWhiteSpace(eventName))
                body["event"] = eventName.Trim();

            Network?.PostReferral(apiBaseUrl, "/api/referrals/qualify", body, apiKey, _ => { });
        }
    }
}
