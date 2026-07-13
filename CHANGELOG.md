# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
