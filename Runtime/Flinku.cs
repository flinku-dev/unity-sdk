using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Networking;

namespace Flinku
{
    public class FlinkuSDK : MonoBehaviour
    {
        private static FlinkuSDK _instance;
        private FlinkuConfig _config;
        private string _apiBaseUrl;
        private string _subdomain;
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
            _config = config;
            // Extract subdomain and apiBaseUrl from baseUrl
            // https://myapp.flku.dev → subdomain=myapp, apiBaseUrl=https://flku.dev
            var uri = new Uri(config.BaseUrl);
            var host = uri.Host; // myapp.flku.dev
            var dotIndex = host.IndexOf('.');
            _subdomain = dotIndex > 0 ? host.Substring(0, dotIndex) : host;
            _apiBaseUrl = $"{uri.Scheme}://{host.Substring(dotIndex + 1)}";
        }

        /// Match deferred deep link on app open
        public void Match(Action<FlinkuLink> onSuccess, Action<string> onError = null)
        {
            StartCoroutine(MatchCoroutine(onSuccess, onError));
        }

        private IEnumerator MatchCoroutine(Action<FlinkuLink> onSuccess, Action<string> onError)
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

                var json = req.downloadHandler.text;
                var response = JsonConvert.DeserializeObject<MatchResponse>(json, JsonSettings);

                if (response != null && response.matched)
                {
                    PlayerPrefs.SetString(MatchCacheKey, JsonConvert.SerializeObject(response.link, JsonSettings));
                    PlayerPrefs.SetFloat(MatchCacheKey + "_time", Time.realtimeSinceStartup);
                    onSuccess?.Invoke(response.link);
                }
                else
                {
                    onSuccess?.Invoke(null);
                }
            }
        }

        /// Create a link programmatically (requires ApiKey)
        public void CreateLink(FlinkuLinkOptions options, Action<FlinkuCreatedLink> onSuccess, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                onError?.Invoke("ApiKey is required to create links");
                return;
            }
            StartCoroutine(CreateLinkCoroutine(options, onSuccess, onError));
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
                    onError?.Invoke(req.error);
                    yield break;
                }

                var link = JsonConvert.DeserializeObject<FlinkuCreatedLink>(req.downloadHandler.text, JsonSettings);
                onSuccess?.Invoke(link);
            }
        }

        /// Reset match cache
        public void Reset()
        {
            PlayerPrefs.DeleteKey(MatchCacheKey);
            PlayerPrefs.DeleteKey(MatchCacheKey + "_time");
        }

        [Serializable]
        private class MatchRequest
        {
            public string userId;
            public string subdomain;
            public string userAgent;
        }

        [Serializable]
        private class MatchResponse
        {
            public bool matched;
            public FlinkuLink link;
        }
    }
}
