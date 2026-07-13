using System;
using System.Collections.Generic;
using System.Linq;
using Flinku;
using Newtonsoft.Json;
using Xunit;

namespace Flinku.Tests
{
    public sealed class MockNetwork : IFlinkuReferralNetwork
    {
        public sealed class RecordedRequest
        {
            public string Url;
            public string Path;
            public Dictionary<string, object> Body;
            public string ApiKey;
        }

        public List<RecordedRequest> Recorded { get; } = new();
        public Dictionary<string, int> StatusByPath { get; } = new();
        public bool FailAll;

        public void PostReferral(
            string apiBaseUrl,
            string path,
            Dictionary<string, object> body,
            string apiKey,
            Action<bool> onComplete)
        {
            Recorded.Add(new RecordedRequest
            {
                Url = apiBaseUrl,
                Path = path,
                Body = new Dictionary<string, object>(body),
                ApiKey = apiKey,
            });

            if (FailAll)
            {
                onComplete(false);
                return;
            }

            var status = StatusByPath.TryGetValue(path, out var s) ? s : 200;
            onComplete(status >= 200 && status <= 299);
        }

        public List<RecordedRequest> Requests(string path) =>
            Recorded.Where(r => r.Path == path || r.Path.EndsWith(path)).ToList();
    }

    public class ReferralLifecycleTests
    {
        private const string ProjectId = "proj_test_1";
        private const string ApiKey = "flk_pk_test_key";
        private const string ApiBaseUrl = "https://flku.dev";

        private InMemoryKeyValueStore _store;
        private MockNetwork _network;
        private readonly List<string> _logs = new();

        public ReferralLifecycleTests()
        {
            FlinkuReferralCore.ResetForTesting();
            _store = new InMemoryKeyValueStore();
            _network = new MockNetwork();
            FlinkuReferralCore.Store = _store;
            FlinkuReferralCore.Network = _network;
            FlinkuReferralCore.LogSink = msg => _logs.Add(msg);
        }

        private static FlinkuLink MatchedLinkWithReferrer() => new FlinkuLink
        {
            Id = "link_abc",
            Slug = "ref-slug",
            ShortUrl = "https://yourapp.flku.dev/ref-slug",
            ProjectId = ProjectId,
            Params = new Dictionary<string, string>
            {
                ["referrerId"] = "user_referrer_1",
                ["referrerLabel"] = "Alice",
            },
        };

        private PendingReferralPayload ReadPending(string projectId)
        {
            var raw = _store.GetString(FlinkuReferralCore.PendingReferralKey(projectId));
            return raw == null ? null : JsonConvert.DeserializeObject<PendingReferralPayload>(raw);
        }

        private void WritePending(string projectId, PendingReferralPayload payload)
        {
            _store.SetString(FlinkuReferralCore.PendingReferralKey(projectId), JsonConvert.SerializeObject(payload));
            _store.SetString(FlinkuReferralCore.ReferralProjectIdKey, projectId);
            _store.SetString(FlinkuReferralCore.PendingReferralIndexKey, projectId);
        }

        [Fact]
        public void MatchWithReferrerIdWritesPendingReferral()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            var pending = ReadPending(ProjectId);
            Assert.NotNull(pending);
            Assert.Equal("user_referrer_1", pending.ReferrerId);
            Assert.Equal("Alice", pending.ReferrerLabel);
            Assert.Equal("link_abc", pending.LinkId);
            Assert.True(pending.MatchedAt > 0);
            Assert.Equal(ProjectId, _store.GetString(FlinkuReferralCore.ReferralProjectIdKey));
        }

        [Fact]
        public void MatchWithoutReferrerIdDoesNotWritePending()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(new FlinkuLink
            {
                Id = "link_plain",
                Slug = "plain-slug",
                ShortUrl = "https://x/y",
                ProjectId = ProjectId,
                Params = new Dictionary<string, string> { ["campaign"] = "summer" },
            });
            Assert.Null(ReadPending(ProjectId));
            Assert.Null(_store.GetString(FlinkuReferralCore.ReferralProjectIdKey));
        }

        [Fact]
        public void ResetKeepsPendingReferral()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            _store.SetString("flinku_match_state", "{}");
            _store.RemoveKey("flinku_match_state");
            var pending = ReadPending(ProjectId);
            Assert.NotNull(pending);
            Assert.Equal("user_referrer_1", pending.ReferrerId);
            Assert.Equal("Alice", pending.ReferrerLabel);
            Assert.Equal("link_abc", pending.LinkId);
        }

        [Fact]
        public void SetUserIdPostsTrackWithCorrectBodyAndBearer()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            _network.StatusByPath["/api/referrals/track"] = 200;
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", ApiKey, ApiBaseUrl, 5000);
            var tracks = _network.Requests("/api/referrals/track");
            Assert.Single(tracks);
            Assert.Equal(ApiKey, tracks[0].ApiKey);
            Assert.Equal(ProjectId, tracks[0].Body["projectId"]);
            Assert.Equal("user_referrer_1", tracks[0].Body["referrerId"]);
            Assert.Equal("new_user_42", tracks[0].Body["newUserId"]);
            Assert.Equal("Alice", tracks[0].Body["referrerLabel"]);
            Assert.Equal("link_abc", tracks[0].Body["linkId"]);
            Assert.Equal(ApiBaseUrl, tracks[0].Url);
        }

        [Fact]
        public void SetUserIdSuccessSetsTrackedFlagAndClearsPending()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            _network.StatusByPath["/api/referrals/track"] = 201;
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", ApiKey, ApiBaseUrl, 5000);
            Assert.True(_store.GetBool(FlinkuReferralCore.ReferralTrackedKey(ProjectId, "new_user_42")));
            Assert.Null(ReadPending(ProjectId));
        }

        [Fact]
        public void SetUserIdNetworkFailureKeepsPending()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            _network.FailAll = true;
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", ApiKey, ApiBaseUrl, 5000);
            Assert.Single(_network.Requests("/api/referrals/track"));
            Assert.NotNull(ReadPending(ProjectId));
            Assert.False(_store.GetBool(FlinkuReferralCore.ReferralTrackedKey(ProjectId, "new_user_42")));
        }

        [Fact]
        public void SetUserIdTwiceAfterSuccessPostsOnlyOnce()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            _network.StatusByPath["/api/referrals/track"] = 200;
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", ApiKey, ApiBaseUrl, 5000);
            Assert.Null(ReadPending(ProjectId));
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", ApiKey, ApiBaseUrl, 5000);
            Assert.Single(_network.Requests("/api/referrals/track"));
        }

        [Fact]
        public void SetUserIdWithoutApiKeySkipsNetworkAndWarnsOnce()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            FlinkuReferralCore.WarnMissingReferralApiKeyOnce(null);
            _store.SetString(FlinkuReferralCore.UserIdKey, "new_user_42");
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", null, ApiBaseUrl, 5000);
            FlinkuReferralCore.WarnMissingReferralApiKeyOnce(null);
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", null, ApiBaseUrl, 5000);
            Assert.Empty(_network.Requests("/api/referrals/track"));
            Assert.NotNull(ReadPending(ProjectId));
            Assert.Equal(1, _logs.Count(l => l.Contains("no apiKey configured")));
            Assert.Equal("new_user_42", _store.GetString(FlinkuReferralCore.UserIdKey));
        }

        [Fact]
        public void ConfigureWithStoredUserIdRetriesTrack()
        {
            WritePending(ProjectId, new PendingReferralPayload
            {
                ReferrerId = "user_referrer_1",
                ReferrerLabel = "Alice",
                LinkId = "link_abc",
                MatchedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
            _store.SetString(FlinkuReferralCore.UserIdKey, "returning_user");
            _network.StatusByPath["/api/referrals/track"] = 200;
            FlinkuReferralCore.TrackReferralInBackground("returning_user", ApiKey, ApiBaseUrl, 5000);
            var tracks = _network.Requests("/api/referrals/track");
            Assert.Single(tracks);
            Assert.Equal("returning_user", tracks[0].Body["newUserId"]);
            Assert.Equal("user_referrer_1", tracks[0].Body["referrerId"]);
            Assert.Equal(ApiKey, tracks[0].ApiKey);
            Assert.True(_store.GetBool(FlinkuReferralCore.ReferralTrackedKey(ProjectId, "returning_user")));
            Assert.Null(ReadPending(ProjectId));
        }

        [Fact]
        public void StalePendingReferralIsDroppedWithoutPost()
        {
            var thirtyOneDaysAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (31 * 24 * 60 * 60);
            WritePending(ProjectId, new PendingReferralPayload
            {
                ReferrerId = "user_referrer_1",
                ReferrerLabel = "Alice",
                LinkId = "link_abc",
                MatchedAt = thirtyOneDaysAgo,
            });
            _store.SetString(FlinkuReferralCore.UserIdKey, "returning_user");
            FlinkuReferralCore.TrackReferralInBackground("returning_user", ApiKey, ApiBaseUrl, 5000);
            Assert.Empty(_network.Requests("/api/referrals/track"));
            Assert.Null(ReadPending(ProjectId));
            Assert.False(_store.GetBool(FlinkuReferralCore.ReferralTrackedKey(ProjectId, "returning_user")));
        }

        [Fact]
        public void QualifyReferralPostsCorrectBody()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            _network.StatusByPath["/api/referrals/track"] = 200;
            _network.StatusByPath["/api/referrals/qualify"] = 200;
            _store.SetString(FlinkuReferralCore.UserIdKey, "new_user_42");
            FlinkuReferralCore.TrackReferralInBackground("new_user_42", ApiKey, ApiBaseUrl, 5000);
            FlinkuReferralCore.QualifyReferralInBackground("first_meal_logged", ApiKey, ApiBaseUrl, 5000);
            var qualifies = _network.Requests("/api/referrals/qualify");
            Assert.Single(qualifies);
            Assert.Equal(ApiKey, qualifies[0].ApiKey);
            Assert.Equal(ProjectId, qualifies[0].Body["projectId"]);
            Assert.Equal("new_user_42", qualifies[0].Body["newUserId"]);
            Assert.Equal("first_meal_logged", qualifies[0].Body["event"]);
        }

        [Fact]
        public void QualifyReferralWithoutUserIdDoesNotCallNetwork()
        {
            FlinkuReferralCore.PersistPendingReferralIfNeeded(MatchedLinkWithReferrer());
            _network.StatusByPath["/api/referrals/qualify"] = 200;
            Assert.Null(_store.GetString(FlinkuReferralCore.UserIdKey));
            FlinkuReferralCore.QualifyReferralInBackground("first_meal_logged", ApiKey, ApiBaseUrl, 5000);
            Assert.Empty(_network.Requests("/api/referrals/qualify"));
        }
    }
}
