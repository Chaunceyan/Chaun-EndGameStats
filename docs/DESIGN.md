# Design

## Product shape

Show one row per player and these columns:

1. player name
2. deaths
3. rescues
4. recovered value
5. valuable damage

Highlight the winner of each category. Ties highlight every tied player. A zero
in a positive category does not earn an award; zero valuable damage may earn the
`Gentle Hands` award only if the player recorded some haul work.

Suggested awards:

- `Guardian Angel`: most teammate rescues
- `Heavy Lifter`: most recovered value contribution
- `Gentle Hands`: least valuable damage among players who carried valuables
- `Made of Spare Parts`: most deaths (comic distinction, not presented as a win)

The board is available throughout active gameplay using a configurable toggle
key (`F3` by default). Values update while it is open. At level completion, freeze
the level snapshot and open the board automatically. Closing the board during a
level affects only visibility; collection continues in the background.

## Definitions

### Deaths

Increment once on the alive-to-dead transition. Reviving does not decrement it.
Guard against duplicate death callbacks by retaining the player's alive/dead
state.

### Teammates rescued

Increment when a player completes a revive on a different player. The reviver is
the actor credited by the host. A revive cancelled before completion and any
self-recovery do not count.

### Recovered value

When a valuable is successfully extracted, split its final current value equally
among all players who participated in handling it. Participation means either:

- directly grabbing the valuable at least once; or
- actively pulling a cart while the valuable was inside it.

Each player is counted once per valuable. Valuables that are not successfully
extracted award nothing. If no participant can be identified, add the value to
the level's unattributed recovered value. Player shares plus unattributed value
must equal the team's actual extracted valuable total.

### Internal haul-work diagnostic

This is a value-weighted carry distance, expressed as `value-metres`:

```text
haul work += max(0, current item value) * valid movement distance
```

Sample on the host while a valuable is actively controlled. Attribute each
movement segment to the controlling player. Ignore teleport-like segments above
a configurable distance threshold, stationary jitter below a small epsilon, and
movement caused solely by the extraction animation. For two-player grabs, split
credit equally between active grabbers for that segment. Use current value so
work performed after damage is not overvalued.

### Valuable damage

Record the positive value loss between two authoritative item-value updates.
Credit the loss to:

1. the player actively grabbing the item at the damage event;
2. otherwise, the last player to release it within the attribution window;
3. otherwise, `environment/unattributed` (shown as a team footnote, not assigned
   to a player).

The attribution window should default to 3 seconds and be configurable. This
captures throws while avoiding blame for an item damaged much later by a monster
or map hazard.

## Multiplayer authority

The host owns event collection and the canonical level snapshot. Clients receive
live updates and the completed snapshot for display. Player identity uses Steam
ID internally and the current display name only for presentation. Late joiners
begin at zero and are marked as having joined mid-level.

## Lifecycle

- Start a new snapshot when a level begins.
- Broadcast updates while the level is active so every client can inspect it.
- Freeze, broadcast, and automatically display it when the level ends.
- Preserve that completed snapshot in the truck/shop until the next level begins.
- Start a fresh snapshot for the next level; optionally retain earlier snapshots
  for a future run-total/history view.

## Open integration work

- Identify stable game methods for death and completed-revive events.
- Confirm the authoritative valuable value field and damage callback.
- Confirm the grabber collection for single and cooperative carries.
- Identify the authoritative level-start and level-end transitions.
- Add configurable `F3` toggle input and automatic end-of-level display.
- Build the Unity board and input/visibility behavior.
