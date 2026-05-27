# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
