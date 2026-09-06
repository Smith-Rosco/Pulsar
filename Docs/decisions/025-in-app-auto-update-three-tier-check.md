# ADR-025: In-App Auto Update — Three-Tier Resilient Check & Mirror Failover

**Status**: Accepted
**Date**: 2026-09-06
**Deciders**: Project owner (milo)
**Related**: ADR-013 (startup timing), openspec change `2026-09-05-in-app-auto-update`

---

## Context

Pulsar is currently a network-zero-dependency desktop app. Shipping releases to
`github.com/Smith-Rosco/Pulsar` means our target users — heavily domestic-China
office environments — routinely hit `api.github.com` timeouts, DNS pollution, and
rate limits. A naive "call the REST API, error out on failure" checker would loop
in a 100%-timeout dead state for exactly those users.

The proven solution shape comes from **StarPie v1.6.8** (MIT licensed): a
three-tier detection cascade plus multi-mirror download failover, born from the
same root-cause analysis we would otherwise have to repeat. We port the
architecture rather than redesign it; attribution recorded here per MIT terms.

## Decision

1. **Three-tier detection cascade, short-circuit on first success:**
   1. GitHub REST API `releases/latest` (5s timeout) — richest payload (tag + assets + SHA256 digests);
   2. Releases Atom feed on the `github.com` main domain (6s timeout) — no auth, no rate limit, survives `api.github.com` blocks;
   3. `releases/latest` 302 redirect probe (5s timeout, redirects NOT followed) — tag only, last resort.

   All tiers failing yields a `NetworkError` state — never a false "outdated" or
   false "up to date" badge (StarPie root-cause 3 lesson: a fake timeout must not
   masquerade as a verdict).

2. **HTTP seam behind `IUpdateHttpGateway`**: one method in, one response out.
   Production implementation `HttpClientUpdateGateway` keeps a redirect-following
   client (asset downloads need CDN redirects) and a non-following one (the probe
   tier must observe the raw Location header). Tests inject fakes — zero network
   flake in the suite.

3. **Header isolation per request kind**: API JSON, Atom XML, HTML probe, and
   binary downloads each carry exactly their own `Accept` header, enforced by the
   gateway contract (caller-supplied, nothing else). GitHub-API-specific headers
   must never leak to XML or binary requests.

4. **Version comparison = exactly three outcomes** (`UpdateVersionComparer`):
   UpToDate / UpdateAvailable / ParseFailed over `{Major}.{Minor}.{Patch}`
   semantics, tolerant of `v` prefixes and dirty suffixes. A running version
   ahead of the release is UpToDate, never an "available update". ParseFailed
   never auto-triggers a download.

5. **Download failover over a centralized mirror table**: official URL first,
   then built-in accelerator templates (`ghfast.top`, `gh-proxy.com`) in order.
   404/502/timeout/mid-stream break on any source advances to the next
   automatically. v1 hardcodes the table (read-only display in settings) — a dead
   mirror is a one-line fix in one file, not a configuration surface.

6. **Integrity before handoff**: SHA256 when the REST tier exposes a digest;
   otherwise a size check with the weaker mode explicitly surfaced to the UI;
   a failed check deletes the partial file and never yields an install handoff.

7. **v1 scope = detect / notify / download / hand off to the installer**: no
   self-replacement of the running exe — file locks and elevation belong to the
   installer change (`2026-09-05-installer-and-portable-packaging`).

## Alternatives Considered

- **REST API only + error surface**: rejected — the exact 100%-timeout dead-loop
  shape StarPie's refactor eliminated; our user base hits it deterministically.
- **User-configurable mirror list (v1)**: rejected — widens the settings surface
  before real demand; the hardcoded table is observable in logs and trivially
  editable in code.

## Consequences

- GitHub asset digests exist only on the REST tier — the Atom/probe paths
  necessarily degrade to size/unverified checks; the UI must say so, never silently.
- Mirror liveness is uncontrollable; mitigation is centralization + startup logs,
  not SLOs we cannot honor.
- The check is fire-and-forget on the delayed startup phase (ADR-013 discipline);
  the UI thread only receives result projections.

## Attribution

Architecture and failure-mode analysis adapted from **StarPie** (MIT License),
`UpdateService` (v1.6.8 refactor). Copyright the original StarPie authors.
