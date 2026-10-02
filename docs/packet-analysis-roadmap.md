# Packet analysis roadmap

These proposals are based on the current parser and runtime, not on assumed access to the game client. Capture stays passive. No raw packets or real player data belong in this repository. Every additional metric needs a documented definition and live Global validation before being presented as authoritative.

## Available foundations

| Feature | Current evidence | Proposed presentation | Limits |
| --- | --- | --- | --- |
| Damage/healing over time | Persisted per-player metric events with observation timestamps, source, target, skill and amount | One-second damage/HPS graphs; selectable time ranges alongside skill events | Packet arrival is not exact server execution time. Burst traffic can bunch events. |
| Skill event sequence | Persisted metric events, original skill catalog and icons | Implemented event timeline with outgoing/incoming directions and chronological list | Hits, multi-target effects and periodic ticks are not separate casts. Skills without a recorded effect may be absent. |
| Crit, positional hits and defensive outcomes | `DamageModifiers` includes Critical, Back, Front, Block, Parry, Evade, Invincible and Perfect | Per-skill rates and counts, incoming mitigation summary | Define event/hit/attempt denominator; validate each flag against Global before publishing inferred meaning. |
| Single-target versus adds | Directed source/target pairs and NPC identities | Boss-only damage, add damage, target-switch timeline and cleave share | Some summon attribution and unknown NPC identities need validation; never silently merge unresolved entities. |
| Direct versus periodic damage | `CombatDeliveryKind` and periodic relation metadata | DoT/HoT share and tick patterns | Cannot infer continuous uptime or original cast count merely from ticks. |
| Incoming damage and defensive healing | Player-target metric events and shield metrics | Who took damage, which attacks hit them, healing received, damage/heal/shield event sequence | Death cause and effective overheal need reliable ordered HP/death observations; amounts alone are insufficient. |
| Shields | Separate ShieldGranted / ShieldAbsorbed metrics | Shield contribution beside healing rather than inflating HPS | Validate attribution, refresh and expiration; granted value is not absorbed damage. |
| Resource changes | Resource events and resource-specific projection | Resource gain/spend by skill; resource timeline | Differentiate observed resource deltas from reconstructed balances; validate supported resources and packet coverage. |
| Cooldowns and charges | `0238`, `2238`, `4738` observations; existing skill monitor | Local-player cooldown availability and charge timeline | Probably limited to observed local-client state. Availability is not proof that a cast occurred or should have occurred. Historical cooldown/state events are not retained in the disk archive today. |
| Buff/debuff intervals | Aura observations and existing playback aura timeline reader | Buff windows aligned with damage bursts; observed apply/refresh/remove events | Needs compact state-event persistence. Missing remove packets and mid-combat capture require open/unknown intervals, not invented uptime. |
| Boss HP progression | Boss focus / entity vital observations | HP curve aligned with damage and damage pauses | Disk archives currently retain final vital snapshots, not full HP history. Requires an explicit compact time-series schema. |
| Connection quality | Passive protocol/TCP latency estimate and capture state | Last observed RTT, estimate source, stale/unknown state, interruption marker | No active probing; passive TCP RTT can differ from game-server processing latency. |

## Recommended next steps

1. **Damage/healing graph and range selection.** Reuse persisted metric events; tie the range to skill and target breakdowns. Define whether a rate uses elapsed range time or the meter's active damage denominator.
2. **Boss-versus-add contribution and per-skill crit/positional breakdown.** Reuse existing pairs and flags; explicitly show unknown classifications. Validate crit and attempt denominators first.
3. **Buff and cooldown overlays.** Persist compact typed observations, never packet payloads, with a versioned disk format. Separate local-player coverage from party coverage.
4. **Incoming damage / death review.** Add a validated death observation and HP timeline before calling an event a killing blow. Present an ordered incident window with damage, healing and shields.
5. **Comparable boss attempts.** Compare the same known boss and selected phase/time ranges. Keep map, duration, group size and missing-capture status visible; do not rank unrelated pulls.

## Research rather than product claims

- Automatic cutscene, invulnerability and mechanic-phase boundaries need a verified semantic signal. Damage inactivity alone cannot identify their cause.
- Cast-start / cast-complete / cancel events need a validated skill-use/control packet association. Do not derive a rotation or cast count by counting hits, ticks or cooldown messages.
- Interrupt and dispel attribution needs verified result codes and source/target association.
- Effective healing and overheal need reliable before/after HP, max HP and event ordering. Packet-reported healing is currently the safe label.
- Position-based dodge mistakes, distance traveled, DPS while moving and AoE avoidance need decoded spatial/animation state with known units and reliable coverage. This assessment does not establish that such data is available.
- Threat/aggro percentage needs explicit threat observations. Boss target changes alone cannot reconstruct a threat table.

## History and timeline behavior in the local draft

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
