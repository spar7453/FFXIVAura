# Deferred Improvements

## Unify observed status lifetime handling

Status: recorded for later implementation.

Party aura timers and party cooldown active-effect timers currently reconcile
`RemainingTime` through separate code paths. Both paths must tolerate coarse,
repeated, temporarily missing, and stale-positive status samples. Keeping the
rules separate risks fixing the same data-quality issue differently in each
feature.

Planned direction:

- Extract a shared status-lifetime reconciler with feature-specific refresh
  policies.
- Do not revive an expired estimate from a single stale-positive sample.
- Accept refreshes only when supported by a new use event, a confirmed
  absent-to-present transition, a meaningful remaining-time increase, or a
  changed status source/generation.
- Include the status source in party-aura timer identity where needed so that
  different casters can be distinguished.
- Preserve snapshot provenance (`live` or `fallback`), capture time, and
  consecutive-repeat information. A fallback sample must never be treated as
  refresh evidence.
- Add bounded anomaly diagnostics for accepted refreshes, suppressed stale
  samples, fallback reads, and status-read failures.
- Add shared replay tests for coarse countdowns, repeated constant values,
  stale positives after expiry, brief read failures, real reapplications, and
  same-status effects from multiple party members.

Live validation must include a continuously refreshed ground effect such as
Sacred Soil. It must stay visible while genuinely refreshed, count down
smoothly, and disappear after leaving or after the effect expires even if the
party list briefly retains the old status slot.
