namespace Flinku
{
    [System.Serializable]
    public class FlinkuConfig
    {
        public string UserId;
        public string BaseUrl; // e.g. https://myapp.flku.dev
        public string ApiKey;  // optional, for creating links
        public int TimeoutMs = 10000;
    }
}
