# FFXIVAura Dalamud Port

This is the first Dalamud-native port of the ACT/HTML FFXIVAura overlay.

## Install Location

Built plugin files are placed in:

`$env:DALAMUD_HOME\Plugins\FFXIVAura`

Dalamud runtime files:

- `FFXIVAura.dll`
- `FFXIVAura.json`
- `Data\abilities.json`

Development source is copied to:

`$env:DALAMUD_HOME\Sources\FFXIVAura-src`

## Current Scope

- Reads current job and effective level from Dalamud `IPlayerState`.
- Hides the overlay during zone loading via Dalamud `ICondition`.
- Loads existing FFXIVAura action/icon data from `Data\abilities.json`.
- Filters actions by current job, level, and supported role actions.
- Reads cooldown, charges, adjusted action id, and highlight state from game `ActionManager`.
- Renders a draggable ImGui icon row with cooldown wedge, seconds, charges, and proc border.

## Command

Use `/fa` in game to open the config window.

## Next Patch Points

- Replace garbled Korean names with Korean client `Action.csv` or another clean KR Action sheet.
- Add editor mode for manually selecting tracked skills per job.
- Add per-job/per-row saved icon positions from the ACT overlay.
- Expand transformed action modeling with Dalamud `GetAdjustedActionId` verification by job.
- Add polish for grayscale/clock wipe styling once in-game rendering is confirmed.
