using System.Collections.Generic;

namespace Flinku
{
    [System.Serializable]
    public class FlinkuLink
    {
        public string Id;
        public string Slug;
        public string ShortUrl;
        public string DeepLink;
        public Dictionary<string, string> Params;
        public string Title;
        public string ClickedAt;
        public string ProjectId;
        public string Subdomain;
        public string MatchType;
    }

    [System.Serializable]
    public class FlinkuLinkOptions
    {
        public string Title;
        public string DeepLink;
        public Dictionary<string, string> Params;
        public string Slug;
        public string DesktopUrl;
        public string UtmSource;
        public string UtmMedium;
        public string UtmCampaign;
        public string InfluencerId;
        public string[] Tags;
        public string CampaignId;
    }

    [System.Serializable]
    public class FlinkuCreatedLink
    {
        public string Id;
        public string Slug;
        public string ShortUrl;
        public string DeepLink;
        public Dictionary<string, string> Params;
    }
}
