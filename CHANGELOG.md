# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 0.7.2 - 2026-09-22

* Optional `CustomDomain` on `FlinkuConfig` (host only) for public short links and clipboard matching.
* `BaseUrl` must be `https://{subdomain}.flku.dev`. Invalid config logs an error and leaves the SDK unconfigured (does not throw).
* Clipboard filter accepts `*.flku.dev`, the baseUrl host, or exact `CustomDomain`.
* `CreateLinkInstant` uses `CustomDomain` when set, else `{subdomain}.flku.dev`.

## 0.7.1 - 2026-09-03

* `CreateLinkInstant` retries the background POST up to three times (1s, 2s, 4s backoff) on transient failures. Adds response status checking and `Debug.LogWarning` on terminal failure. Terminal HTTP errors (400, 403, 409) are not retried.

## 0.7.0 - 2026-07-21

* `FlinkuSDK.Instance.ResetAll()` — testing-only full local wipe (match cache, user id, pending referral, tracked-once flags). Does not change `Reset()` behaviour.
* Pending referral index entry is removed when a pending referral record expires by TTL.

## 0.6.0

* Pending referral record (`flinku_pending_referral_{projectId}`) survives `Reset()`
* Track once per `referral_tracked_{projectId}_{userId}`; retry on init when userId stored
* 30-day pending TTL; 12 xUnit tests for referral lifecycle

## 0.2.0

* `SetUserId` — store app user id and auto-track referrals from cached match `referrerId`
* `QualifyReferral` — mark a referred user as qualified for an optional event

## 0.1.2 - Support publishable API keys (flk_pk_). Debug-mode warning when a secret key is embedded. Surface Allowed Domains errors clearly.

## 0.1.1 - Add createLinkInstant for instant link creation without waiting for server

## [0.2.0] - 2026-05-17

### Added

- Clipboard-based deferred deep linking: `Match` checks the system clipboard for Flinku URLs before the fingerprint API.
- `MatchType` on `FlinkuLink`, populated from the API `matchType` field.
- `Subdomain` on `FlinkuConfig`, set automatically from `BaseUrl` on `Initialize`.

## [0.1.0] - 2026-03-29

### Added

- Initial release: `FlinkuSDK` with deferred deep link `Match`, optional `CreateLink` (API key), match cache with TTL, and reset.
- Models: `FlinkuConfig`, `FlinkuLink`, `FlinkuLinkOptions`, `FlinkuCreatedLink`.
- Unity Package Manager manifest (`dev.flinku.sdk`) and assembly definition `Flinku.Runtime`.
