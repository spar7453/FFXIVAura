# Deferred Improvements

## Unify observed status lifetime handling

Status: core reconciliation, explicit present/absent/unavailable observations,
snapshot provenance, bounded decision diagnostics, and replay tests are
implemented. Live party and alliance validation remains.

Party aura timers and party cooldown active-effect timers currently reconcile
`RemainingTime` through separate code paths. Both paths must tolerate coarse,
repeated, temporarily missing, and stale-positive status samples. Keeping the
rules separate risks fixing the same data-quality issue differently in each
feature.

Planned direction:

- Shared status-lifetime reconciliation now uses feature-specific refresh
  policies.
- Expired estimates no longer revive from a single stale-positive sample.
- Refreshes require a new use event, an absent-to-present transition, or a
  meaningful remaining-time increase accepted by the feature policy.
- Party-aura timer identity includes the status source.
- Snapshot provenance is retained and fallback samples cannot confirm a
  refresh.
- Failed reads no longer behave like confirmed removals; their existing
  estimates continue to decay until a successful read confirms absence.
- Profiling records accepted refreshes, suppressed stale samples, fallback
  reads, last decisions, and alliance roster-order hashes.
- Replay tests cover coarse countdowns, repeated constants, stale positives,
  fallback samples, removals, and real reapplications.

Live validation must include a continuously refreshed ground effect such as
Sacred Soil. It must stay visible while genuinely refreshed, count down
smoothly, and disappear after leaving or after the effect expires even if the
party list briefly retains the old status slot.
