# End Game Stats Board for R.E.P.O.

A live, multiplayer per-level scoreboard for **R.E.P.O.**. The board focuses on
what players actually did instead of only showing the team's extracted total.
It can be opened at any time during a level and is shown automatically when the
level ends.

## How to use

- Press **F3** at any time during a level to open or close the stats board.
- The board opens automatically when the level ends.
- The toggle key can be changed in the r2modman config editor under
  `Display > ToggleKey`.

## Stats

| Stat | Better | Definition |
| --- | --- | --- |
| Deaths | Lower | Confirmed deaths during the level. A later revive does not erase a death. |
| Teammates rescued | Higher | A revive completed on another player. Self-recovery does not count. |
| Enemy kills | Higher | The enemy died shortly after the player's last weapon hit, object impact, or direct physical interaction. |
| Recovered value | Higher | Final extracted value split equally among players who handled the valuable or transported its cart. |
| Valuable damage | Lower | Value lost from valuables attributed to the responsible player. |

The board shows live raw values and gives the category leaders a title. Recovered
value is credited only after an item is successfully extracted, then split equally
among the players who participated in handling it.

See [docs/DESIGN.md](docs/DESIGN.md) for event attribution and multiplayer rules.

## Feedback and bug reports

Found a bug or have a suggestion? Please
[open a GitHub issue](https://github.com/Chaunceyan/Chaun-EndGameStats/issues).
For bugs, include the mod version, whether you were the host or a client, what
happened, and your `BepInEx/LogOutput.log` if available.

## Building

Install the .NET 8 SDK, then provide paths to the game and an r2modman profile
using either environment variables:

```sh
export REPO_GAME_DIR="/path/to/REPO"
export R2_PROFILE_DIR="/path/to/r2modman/profile"
dotnet build src/EndGameStats/EndGameStats.csproj -c Release
```

or copy `Directory.Build.local.props.example` to
`Directory.Build.local.props` and edit the paths. The local file is ignored by
Git.

## License

Released under the [MIT License](LICENSE).
