namespace Flinku
{
    [System.Serializable]
    public class FlinkuConfig
    {
        public string UserId;
        /// <summary>Project subdomain URL, e.g. https://myapp.flku.dev</summary>
        public string BaseUrl; // e.g. https://myapp.flku.dev
        public string Subdomain; // set automatically from BaseUrl on Initialize
        /// <summary>
        /// Optional API key for creating links. Accepts publishable keys (flk_pk_) or secret keys (flk_live_).
        /// Use your publishable key (flk_pk_) in apps. Never embed your secret key (flk_live_).
        /// </summary>
        public string ApiKey;
        public int TimeoutMs = 10000;
    }
}
