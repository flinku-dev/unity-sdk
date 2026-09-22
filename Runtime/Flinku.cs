using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Networking;

namespace Flinku
{
    public class FlinkuSDK : MonoBehaviour
    {
        private static FlinkuSDK _instance;
        private static bool _secretKeyWarningShown;
        private FlinkuConfig _config;
        private string _apiBaseUrl;
        private string _subdomain;
        private string _baseUrlHost;
        private string _customDomain;
        private bool _configured;
        private const string MatchCacheKey = "flinku_match_state";
        private const float MatchCacheTtlSeconds = 300f;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public static FlinkuSDK Instance => _instance;

        public static FlinkuSDK Initialize(FlinkuConfig config)
        {
            if (_instance != null) return _instance;
            var go = new GameObject("FlinkuSDK");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FlinkuSDK>();
            _instance.Setup(config);
            return _instance;
        }

        private void Setup(FlinkuConfig config)
        {
            // Validate without throwing: log and leave unconfigured (C# / Unity norms).
            if (!FlinkuUrlHelpers.TryParseBaseUrl(
                    config?.BaseUrl,
                    out var subdomain,
                    out var apiBaseUrl,
                    out var baseUrlHost,
                    out var baseUrlError))
            {
                Debug.LogError("[Flinku] " + (baseUrlError ?? FlinkuUrlHelpers.InvalidBaseUrlMessage));
                _configured = false;
                _config = null;
                return;
            }

            if (!FlinkuUrlHelpers.TryParseCustomDomain(
                    config.CustomDomain,
                    out var customDomain,
                    out var customDomainError))
            {
                Debug.LogError("[Flinku] " + customDomainError);
                _configured = false;
                _config = null;
                return;
            }

            _config = config;
            _subdomain = subdomain;
            _config.Subdomain = _subdomain;
            _config.CustomDomain = customDomain;
            _apiBaseUrl = apiBaseUrl;
            _baseUrlHost = baseUrlHost;
            _customDomain = customDomain;
            _configured = true;

            FlinkuReferralCore.Store = new PlayerPrefsKeyValueStore();
            FlinkuReferralCore.Network = new UnityReferralNetwork(this);

            if (!string.IsNullOrEmpty(config.ApiKey) &&
                config.ApiKey.StartsWith("flk_live_") &&
                !_secretKeyWarningShown)
            {
                _secretKeyWarningShown = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "FLINKU WARNING: You are embedding a secret key (flk_live_) in your app. " +
                    "Anyone can extract it and gain full access to your links. " +
                    "Use your publishable key (flk_pk_) instead - find it in your project settings at app.flinku.dev.");
#endif
            }

            var storedUserId = FlinkuReferralCore.Store.GetString(FlinkuReferralCore.UserIdKey)?.Trim();
            if (!string.IsNullOrEmpty(storedUserId))
            {
                FlinkuReferralCore.TrackReferralInBackground(
                    storedUserId, _config.ApiKey, _apiBaseUrl, _config.TimeoutMs);
            }
        }

        private bool EnsureConfigured()
        {
            if (_configured && _config != null) return true;
            Debug.LogError("[Flinku] Not configured. Call FlinkuSDK.Initialize() with a valid BaseUrl first.");
            return false;
        }

        private static string LinkCreationErrorMessage(string body, long statusCode)
        {
            if (string.IsNullOrEmpty(body))
                return $"Failed to create link: HTTP {statusCode}";
            try
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                if (json != null)
                {
                    if (json.TryGetValue("error", out var error) && error is string errorText && !string.IsNullOrEmpty(errorText))
                        return errorText;
                    if (json.TryGetValue("message", out var message) && message is string messageText && !string.IsNullOrEmpty(messageText))
                        return messageText;
                }
            }
            catch { }
            return body;
        }

        public void Match(Action<FlinkuLink> onSuccess, Action<string> onError = null)
        {
            if (!EnsureConfigured())
            {
                onSuccess?.Invoke(null);
                return;
            }
            StartCoroutine(MatchEntryCoroutine(onSuccess, onError));
        }

        private IEnumerator MatchEntryCoroutine(Action<FlinkuLink> onSuccess, Action<string> onError)
        {
            var clipText = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrEmpty(clipText) &&
                FlinkuUrlHelpers.IsClipboardUrlAccepted(clipText, _baseUrlHost, _customDomain))
            {
                GUIUtility.systemCopyBuffer = "";
                FlinkuLink clipboardLink = null;
                yield return MatchWithClipboard(clipText, link => clipboardLink = link);
                if (clipboardLink != null)
                {
                    onSuccess?.Invoke(clipboardLink);
                    yield break;
                }
                yield return MatchWithFingerprint(onSuccess, onError);
                yield break;
            }
            yield return MatchWithFingerprint(onSuccess, onError);
        }

        private IEnumerator MatchWithClipboard(string clipUrl, Action<FlinkuLink> callback)
        {
            var body = JsonConvert.SerializeObject(new ClipboardMatchRequest
            {
                subdomain = _config.Subdomain,
                clipboardUrl = clipUrl
            }, JsonSettings);

            using (var req = new UnityWebRequest($"{_apiBaseUrl}/api/match", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.Max(1, _config.TimeoutMs / 1000);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(null);
                    yield break;
                }

                var response = JsonConvert.DeserializeObject<MatchResponse>(req.downloadHandler.text, JsonSettings);
                if (response != null && response.matched && response.link != null)
                {
                    ApplyMatchType(response, response.link);
                    PersistMatch(response.link);
                    callback?.Invoke(response.link);
                }
                else callback?.Invoke(null);
            }
        }

        private IEnumerator MatchWithFingerprint(Action<FlinkuLink> onSuccess, Action<string> onError)
        {
            var cached = PlayerPrefs.GetString(MatchCacheKey, "");
            var cachedTime = PlayerPrefs.GetFloat(MatchCacheKey + "_time", 0);
            if (!string.IsNullOrEmpty(cached) && Time.realtimeSinceStartup - cachedTime < MatchCacheTtlSeconds)
            {
                var cachedLink = JsonConvert.DeserializeObject<FlinkuLink>(cached, JsonSettings);
                onSuccess?.Invoke(cachedLink);
                yield break;
            }

            var body = JsonConvert.SerializeObject(new MatchRequest
            {
                userId = _config.UserId,
                subdomain = _subdomain,
                userAgent = $"unity/{Application.version}"
            }, JsonSettings);

            using (var req = new UnityWebRequest($"{_apiBaseUrl}/api/match", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.Max(1, _config.TimeoutMs / 1000);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(req.error);
                    onSuccess?.Invoke(null);
                    yield break;
                }

                var response = JsonConvert.DeserializeObject<MatchResponse>(req.downloadHandler.text, JsonSettings);
                if (response != null && response.matched && response.link != null)
                {
                    ApplyMatchType(response, response.link);
                    PersistMatch(response.link);
                    onSuccess?.Invoke(response.link);
                }
                else onSuccess?.Invoke(null);
            }
        }

        private static void ApplyMatchType(MatchResponse response, FlinkuLink link)
        {
            if (string.IsNullOrEmpty(link.MatchType) && !string.IsNullOrEmpty(response.matchType))
                link.MatchType = response.matchType;
        }

        private static void PersistMatch(FlinkuLink link)
        {
            PlayerPrefs.SetString(MatchCacheKey, JsonConvert.SerializeObject(link, JsonSettings));
            PlayerPrefs.SetFloat(MatchCacheKey + "_time", Time.realtimeSinceStartup);
            PlayerPrefs.Save();
            FlinkuReferralCore.Store.SetString(
                FlinkuReferralCore.MatchResultKey,
                JsonConvert.SerializeObject(link, JsonSettings));
            FlinkuReferralCore.PersistPendingReferralIfNeeded(link);
        }

        public FlinkuCreatedLink CreateLinkInstant(FlinkuLinkOptions options)
        {
            if (!EnsureConfigured())
                return null;

            var slug = GenerateInstantSlug(options.Title);
            if (string.IsNullOrEmpty(_config.ApiKey))
                Debug.LogError("Flinku: ApiKey is required to create links");
            else
                StartCoroutine(CreateLinkInstantBackgroundCoroutine(options, slug));

            return new FlinkuCreatedLink
            {
                Id = "",
                Slug = slug,
                ShortUrl = FlinkuUrlHelpers.BuildPublicShortUrl(_subdomain, slug, _customDomain),
                DeepLink = options.DeepLink,
                Params = options.Params
            };
        }

        public void SetUserId(string userId)
        {
            if (!EnsureConfigured()) return;
            if (string.IsNullOrWhiteSpace(userId)) return;
            var id = userId.Trim();
            FlinkuReferralCore.WarnMissingReferralApiKeyOnce(_config?.ApiKey);
            FlinkuReferralCore.Store.SetString(FlinkuReferralCore.UserIdKey, id);
            FlinkuReferralCore.TrackReferralInBackground(
                id, _config?.ApiKey, _apiBaseUrl, _config?.TimeoutMs ?? 10000);
        }

        public void QualifyReferral(string eventName = null)
        {
            if (!EnsureConfigured()) return;
            FlinkuReferralCore.WarnMissingReferralApiKeyOnce(_config?.ApiKey);
            FlinkuReferralCore.QualifyReferralInBackground(
                eventName, _config?.ApiKey, _apiBaseUrl, _config?.TimeoutMs ?? 10000);
        }

        public void CreateLink(FlinkuLinkOptions options, Action<FlinkuCreatedLink> onSuccess, Action<string> onError = null)
        {
            if (!EnsureConfigured())
            {
                onError?.Invoke("Flinku is not configured");
                return;
            }
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                onError?.Invoke("ApiKey is required to create links");
                return;
            }
            StartCoroutine(CreateLinkCoroutine(options, onSuccess, onError));
        }

        private static string GenerateInstantSlug(string title)
        {
            var basePart = (title ?? "").ToLowerInvariant().Trim();
            basePart = Regex.Replace(basePart, @"[^a-z0-9\s-]", "");
            basePart = Regex.Replace(basePart, @"\s+", "-");
            basePart = Regex.Replace(basePart, @"-+", "-").Trim('-');
            if (string.IsNullOrEmpty(basePart)) basePart = "link";
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var rng = new System.Random();
            var suffix = new string(Enumerable.Range(0, 4).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
            return $"{basePart}-{suffix}";
        }

        private IEnumerator CreateLinkInstantBackgroundCoroutine(FlinkuLinkOptions options, string slug)
        {
            options.Slug = slug;
            var body = JsonConvert.SerializeObject(options, JsonSettings);
            var backoffs = new[] { 1f, 2f, 4f };
            var lastMessage = "Failed to create link";
            // Retries run in the background; if the process dies mid-retry, attempts stop.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using (var req = new UnityWebRequest($"{_apiBaseUrl}/api/links", "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", $"Bearer {_config.ApiKey}");
                    req.timeout = Mathf.Max(1, _config.TimeoutMs / 1000);
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success &&
                        req.responseCode >= 200 && req.responseCode <= 299)
                    {
                        yield break;
                    }

                    lastMessage = LinkCreationErrorMessage(req.downloadHandler?.text, req.responseCode);
                    var retryable = req.result != UnityWebRequest.Result.Success ||
                                    req.responseCode == 429 ||
                                    req.responseCode >= 500;
                    if (!retryable || attempt >= 2)
                    {
                        Debug.LogWarning($"[Flinku] createLinkInstant background error: {lastMessage}");
                        yield break;
                    }
                }

                if (attempt < 2)
                {
                    yield return new WaitForSeconds(backoffs[attempt]);
                }
            }
        }

        private IEnumerator CreateLinkCoroutine(FlinkuLinkOptions options, Action<FlinkuCreatedLink> onSuccess, Action<string> onError)
        {
            var body = JsonConvert.SerializeObject(options, JsonSettings);
            using (var req = new UnityWebRequest($"{_apiBaseUrl}/api/links", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {_config.ApiKey}");
                req.timeout = Mathf.Max(1, _config.TimeoutMs / 1000);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(LinkCreationErrorMessage(req.downloadHandler?.text, req.responseCode));
                    yield break;
                }
                var link = JsonConvert.DeserializeObject<FlinkuCreatedLink>(req.downloadHandler.text, JsonSettings);
                onSuccess?.Invoke(link);
            }
        }

        public void Reset()
        {
            PlayerPrefs.DeleteKey(MatchCacheKey);
            PlayerPrefs.DeleteKey(MatchCacheKey + "_time");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears all Flinku local state, including referral attribution and stored user id.
        /// <b>Testing only — do not call in production.</b> Clearing attribution destroys
        /// real referral data. <see cref="Reset"/> was narrowed in 0.6.0 so production
        /// deep-link handling does not wipe referrals; use ResetAll only for a full wipe
        /// in development or QA.
        /// </summary>
        public void ResetAll()
        {
            Reset();
            FlinkuReferralCore.ClearAllReferralState();
        }

        private sealed class UnityReferralNetwork : IFlinkuReferralNetwork
        {
            private readonly FlinkuSDK _sdk;
            public UnityReferralNetwork(FlinkuSDK sdk) => _sdk = sdk;

            public void PostReferral(
                string apiBaseUrl,
                string path,
                Dictionary<string, object> body,
                string apiKey,
                Action<bool> onComplete)
            {
                _sdk.StartCoroutine(PostCoroutine(apiBaseUrl, path, body, apiKey, onComplete));
            }

            private static IEnumerator PostCoroutine(
                string apiBaseUrl,
                string path,
                Dictionary<string, object> body,
                string apiKey,
                Action<bool> onComplete)
            {
                var json = JsonConvert.SerializeObject(body, JsonSettings);
                using (var req = new UnityWebRequest($"{apiBaseUrl}{path}", "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    yield return req.SendWebRequest();
                    var ok = req.result == UnityWebRequest.Result.Success &&
                             req.responseCode >= 200 && req.responseCode <= 299;
                    onComplete?.Invoke(ok);
                }
            }
        }

        [Serializable]
        private class MatchRequest
        {
            public string userId;
            public string subdomain;
            public string userAgent;
        }

        [Serializable]
        private class ClipboardMatchRequest
        {
            public string subdomain;
            public string clipboardUrl;
        }

        [Serializable]
        private class MatchResponse
        {
            public bool matched;
            public string matchType;
            public FlinkuLink link;
        }
    }
}
