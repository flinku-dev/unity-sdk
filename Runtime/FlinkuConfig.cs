namespace Flinku
{
    [System.Serializable]
    public class FlinkuConfig
    {
        public string UserId;
        public string BaseUrl; // e.g. https://myapp.flku.dev
        public string Subdomain; // set automatically from BaseUrl on Initialize
        public string ApiKey;  // optional, for creating links
        public int TimeoutMs = 10000;
    }
}
