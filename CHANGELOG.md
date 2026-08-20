# Changelog

## 0.4.1

- Added the public GitHub repository and issue tracker to the package page.
- Added public build instructions and released the source under the MIT License.

## 0.4.0

- Added per-player enemy kills and the `物理超度师` title for the level leader.
- Attributes kills to recent weapon hits, object impacts, and direct handling,
  including throwing an enemy into a death pit.
- Added a configurable 10-second enemy-kill attribution window.

## 0.3.6

- Fixed rescue counts rapidly increasing when the game repeats `ReviveRPC`
  during a single revive sequence.
- A death can now produce at most one rescue credit; truck revives consume the
  death state without awarding a teammate rescue.

## 0.3.5

- Fixed the stats board being empty or incomplete for non-host players.
- Every peer now derives its local board from the game's replicated RPC events,
  including grabs, deaths, revives, valuable damage, and extraction state.

## 0.3.4

- Added prominent F3 usage instructions to the Thunderstore README.
- Updated the README to reflect the released in-game feature set.

## 0.3.3

- Fixed the top terminal title being covered by the custom CRT frame.
- Draws the title after the bezel and reserves dedicated vertical space.

## 0.3.2

- Reworked the theme from a modern green panel into an inset monochrome CRT
  terminal with a heavy bezel, phosphor-green text, terminal font, and stronger
  scanlines.

## 0.3.1

- Replaced the 10 Hz whole-scene valuable scan with event-driven grab tracking.
- Uses the game's maintained valuable list instead of `FindObjectsOfType`.
- Reduced cart participation fallback tracking to once per second.

## 0.3.0

- Added a wasteland-electronics terminal theme with a dark green panel, amber
  frame, alternating rows, warning-colored titles, and subtle scanlines.

## 0.2.2

- Moved awards into a dedicated title column on each player's table row.
- Multiple titles are separated with a slash instead of creating extra lines.

## 0.2.1

- Added funny level titles for the leaders in deaths, rescues, recovered value,
  and goods damage (including `负资产` for the biggest goods-damage total).
- Tied leaders share a title, and zero-value categories award nobody.

## 0.2.0

- Added per-player recovered value for successfully extracted valuables.
- A valuable's final value is split equally among players who directly grabbed
  it or pulled a cart while it was inside.
- Replaced the displayed haul-work score with recovered value.

## 0.1.3

- Fixed a duplicate `Semibot` row appearing after a level transition while the
  player's Steam identity was still initializing.
- Player rows now use the stable Photon actor number for the current connection.

## 0.1.2

- Added Simplified Chinese UI localization.
- Added automatic language detection and a manual `Display.Language` setting.

## 0.1.1

- Replaced space-padded scoreboard text with fixed-width GUI columns so headers
  and values align correctly with Unity's proportional font.

## 0.1.0 - prototype

- Added a live `F3` per-level stats board.
- Added host-side tracking for deaths and completed teammate revives.
- Added value-weighted valuable carry distance (`haul work`).
- Added valuable damage attribution to the current or recent carrier.
- Preserved and automatically displayed the completed board between levels.
