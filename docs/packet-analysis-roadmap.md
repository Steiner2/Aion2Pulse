# Packet analysis roadmap

The preview implementations and research proposals are based on the current parser and runtime, not on assumed access to the game client. Capture stays passive. No raw packets or real player data belong in this repository. Every additional metric needs a documented definition and live Global validation before being presented as authoritative.

## Available foundations

| Feature | Current evidence | Preview presentation | Limits |
| --- | --- | --- | --- |
| Damage/healing over time | Persisted per-player metric events with observation timestamps, source, target, skill and amount | Implemented adaptive damage/HPS graphs and selectable time ranges | Packet arrival is not exact server execution time. Burst traffic can bunch events. |
| Skill event sequence | Persisted metric events, original skill catalog and icons | Implemented event timeline with outgoing/incoming directions and chronological list | Hits, multi-target effects and periodic ticks are not separate casts. Skills without a recorded effect may be absent. |
| Crit, positional hits and defensive outcomes | `DamageModifiers` includes Critical, Back, Front, Block, Parry, Evade, Invincible and Perfect | Implemented per-skill modifier fractions, perfect/defensive-perfect counts and incoming reported amounts | Define event/hit/attempt denominator; validate each flag against Global before publishing inferred meaning. |
| Single-target versus adds | Directed source/target pairs and NPC identities | Implemented counterpart filters, category shares and an observed target/source change timeline | Some summon attribution and unknown NPC identities need validation; never silently merge unresolved entities. |
| Direct versus periodic damage | `CombatDeliveryKind` and periodic relation metadata | Implemented delivery amounts/shares, per-skill DoT/HoT totals and effect timeline | Cannot infer continuous uptime or original cast count merely from ticks. |
| Incoming damage and defensive healing | Player-target metric events and shield metrics | Who took damage, which attacks hit them, healing received, damage/heal/shield event sequence | Death cause and effective overheal need reliable ordered HP/death observations; amounts alone are insufficient. |
| Shields | Separate ShieldGranted / ShieldAbsorbed metrics | Shield contribution beside healing rather than inflating HPS | Validate attribution, refresh and expiration; granted value is not absorbed damage. |
| Resource changes | Resource events and resource-specific projection | Implemented resource gain/spend totals and chronological skill observations | Differentiate observed resource deltas from reconstructed balances; validate supported resources and packet coverage. |
| Cooldowns and charges | `0238`, `2238`, `4738` observations; existing skill monitor | Implemented cooldown interval projections, charge/action markers and chronological state | Probably limited to observed local-client state. Availability is not proof that a cast occurred or should have occurred. Version-2 disk archives retain bounded typed state events; version-1 archives show them as unavailable. |
| Buff/debuff intervals | Aura observations and existing playback aura timeline reader | Implemented observed buff/debuff/unknown-aura windows aligned with damage bursts | Implemented compact observed windows in version-2 archives. Missing remove packets and mid-combat capture require open/unknown intervals, not invented uptime. |
| Boss HP progression | Boss focus / entity vital observations | HP curve aligned with damage and damage pauses | Version-2 archives retain observed HP points; the review shows curves for up to three bosses and a chronological list. Missing observations are not interpolated into verified phases. |
| Connection quality | Passive protocol/TCP latency estimate and capture state | Implemented RTT tooltip with source, sample age, expiration and unknown state; interrupted archives remain marked | No active probing; passive TCP RTT can differ from game-server processing latency. |

## Implemented in preview.3

1. **Damage/healing graph and range selection.** Reuse persisted metric events; tie the range to skill and target breakdowns. Define whether a rate uses elapsed range time or the meter's active damage denominator.
2. **Boss-versus-add contribution and per-skill crit/positional breakdown.** Reuse existing pairs and flags; explicitly show unknown classifications. Validate crit and attempt denominators first.
3. **Buff and cooldown overlays.** Implemented compact typed observations and bounded aura windows in a versioned disk format, without packet payloads. Separate local-player coverage from party coverage.
4. **Incoming damage / death review.** Incoming incident windows and HP snapshots are implemented. A killing blow remains unknown without validated death ordering. Present an ordered incident window with damage, healing and shields.
5. **Comparable boss attempts.** Compare the same known boss and selected phase/time ranges. Keep map, duration, group size and missing-capture status visible; do not rank unrelated pulls.

Preview.3 also adds contextual hover help across settings, the overlay, history,
analysis tabs, graph points and timeline markers. Settings → Skills restores
access to the monitor icon selection and its separate scale. The counterpart filter applies
to metric events; support state remains scoped to the selected player and range.
Incoming review shows full-range totals plus a five-second incident window.
Boss comparisons include DPS, HPS, totals, group size and overlap.

Observed gaps above three seconds describe this player's positive-damage event
spacing. They do not identify a cutscene and do not change the overlay's configured
pause threshold. Cooldown bars project a received remaining-time observation;
later resets, casts or missed updates can invalidate that projection. At most
12 support/target lanes are drawn; chronological lists retain the observations.
Unknown aura disposition and missing effect names are explicitly labeled.

Support persistence caps interpreted state at 100,000 events and aura windows at
100,000. Aura inspection prioritizes player identities and known bosses across
at most 32 entities. Reaching either cap is shown as incomplete coverage.

## Research rather than product claims

- Automatic cutscene, invulnerability and mechanic-phase boundaries need a verified semantic signal. Damage inactivity alone cannot identify their cause.
- Cast-start / cast-complete / cancel events need a validated skill-use/control packet association. Do not derive a rotation or cast count by counting hits, ticks or cooldown messages.
- Interrupt and dispel attribution needs verified result codes and source/target association.
- Effective healing and overheal need reliable before/after HP, max HP and event ordering. Packet-reported healing is currently the safe label.
- Position-based dodge mistakes, distance traveled, DPS while moving and AoE avoidance need decoded spatial/animation state with known units and reliable coverage. This assessment does not establish that such data is available.
- Threat/aggro percentage needs explicit threat observations. Boss target changes alone cannot reconstruct a threat table.

## History and timeline behavior

- Independently retain the newest 50 known boss encounters and 50 normal pulls, including after restart. Normal pulls cannot evict boss records. Older files already deleted by a prior version cannot be recovered.
- Boss identification uses the existing NPC/boss-focus classification; it does not distinguish dungeon bosses from open-world bosses reliably. Dungeon-specific protection should use a verified map catalog classification, not an arbitrary map ID or instance ID heuristic.
- Bosses are the initial history filter; All fights, Normal pulls and Incomplete remain available.
- Split view starts with a 270 px list and a draggable divider; narrow windows stack list and detail.
- All three overlays use tight columns: 300 px automatic width for a single metric mode, 420 px for DPS/HPS together, before UI scaling. Totals use 48 px and rates use 60 px; user width is saved. Names use the remaining space with marquee overflow instead of a large reserved name column.
- The skill event timeline uses the original encounter offsets, including pauses, and works on restored encounters without a raw journal. It exposes outgoing/incoming observed effects with real catalog icons.

## Code references

- [Combat event facts](../meter/src/Aion2Flow.SceneRuntime/Projection/CombatDetailEvent.cs)
- [Delivery and metric kinds](../meter/src/Aion2Flow.SceneRuntime/Combat/CombatContribution.cs)
- [Damage modifiers](../meter/src/Aion2Flow.Protocol/Combat/DamageModifiers.cs)
- [Typed observations](../meter/src/Aion2Flow.SceneRuntime/Observation/DomainObservations.cs)
- [Packet parsers](../meter/src/Aion2Flow.Protocol/Packets/)
- [Aura playback reader](../meter/src/Aion2Flow.SceneRuntime/Playback/ScenePlaybackAuraTimelineReader.cs)
- [Persisted encounter format](../meter/src/Aion2Flow.SceneRuntime/Archive/StoredEncounter.cs)
- [Skill event timeline](../meter/src/Aion2Flow/Views/SkillEventTimelineView.cs)
