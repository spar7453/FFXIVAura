# FFXIVAura

FFXIVAura is a Dalamud-native cooldown and aura tracking overlay for Final Fantasy XIV. It started as a replacement for an ACT/HTML WeakAura-style overlay and is currently focused on the Korean FFXIV client environment.

The goal is simple: show only the skills, buffs, debuffs, charges, cooldowns, and proc states you actually care about, in movable icon windows that can behave like a lightweight WeakAura setup.

> Status: early custom plugin. It is usable for personal testing, but the data model and UI are still evolving.

## Features

- Dalamud-native ImGui overlay, no ACT browser overlay required.
- `/fa` command opens the settings window.
- Multiple overlay windows.
- Per-window options for size, gap, font scale, position, role, and display condition.
- Overlay roles:
  - Skill cooldowns
  - Player buffs
  - Target debuffs
  - Party buffs
- Per-job tracked skill lists.
- Per-job icon positions.
- Manual icon dragging inside the overlay grid.
- Row-based display order editor.
- Icon alignment and position reset tools.
- Effective level filtering for synced content.
- Role action support is always enabled.
- Cooldown display using Dalamud ActionManager data.
- Charge count display for charge-based skills.
- Adjusted action display through game action adjustment data.
- Unavailable/proc-gated action desaturation.
- Ready and adjusted-action highlight options.
- Keybind label display for tracked hotbar actions.
- Hide overlay during zone load.
- Optional display conditions:
  - Always
  - In combat
  - Out of combat
  - Cooling/active only
  - Ready/missing only
- Aura search and tracking for recently seen buffs/debuffs.

## Target Environment

This repository is maintained for a personal Korean FFXIV setup.

Current local development assumptions:

- Windows
- XIVLauncherKR
- Dalamud API level 15
- .NET SDK with `net10.0-windows` support
- Dalamud hook assemblies installed under a path like:

```text
C:\Users\<user>\AppData\Roaming\XIVLauncherKR\addon\Hooks\15.0.2.2\
```

The project file currently references Dalamud assemblies by absolute local paths. If your Dalamud install path is different, update the `HintPath` values in `FFXIVAura.csproj` before building.

## Installation

Build the project, then copy the runtime files into your local Dalamud plugin folder.

Expected plugin folder:

```text
$env:DALAMUD_HOME\Plugins\FFXIVAura
```

Required runtime files:

```text
FFXIVAura.dll
FFXIVAura.json
Data\abilities.json
```

After copying the files, reload Dalamud or restart the game. In game, open the settings window with:

```text
/fa
```

## Build

From the repository directory:

```powershell
dotnet build .\FFXIVAura.csproj -c Release
```

Build output:

```text
bin\Release\net10.0-windows\FFXIVAura.dll
```

Manual deploy example:

```powershell
$src = "bin\Release\net10.0-windows"
$dst = "$env:DALAMUD_HOME\Plugins\FFXIVAura"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item -Force "$src\FFXIVAura.dll" "$dst\FFXIVAura.dll"
Copy-Item -Force "$src\FFXIVAura.pdb" "$dst\FFXIVAura.pdb" -ErrorAction SilentlyContinue
Copy-Item -Force ".\FFXIVAura.json" "$dst\FFXIVAura.json"
Copy-Item -Recurse -Force "$src\Data" "$dst\Data"
```

## Configuration Overview

Open the settings window with `/fa`.

### General

- Enable or disable the plugin.
- Lock overlay movement.
- Hide overlays during zone loading.
- Check current job, effective level, loaded action count, and command name.

### Overlay

- Add or delete icon windows.
- Rename each window.
- Choose a window role:
  - Skill
  - Player buff
  - Target debuff
  - Party buff
- Choose a display condition.
- Change icon size, gap, overlay width, overlay height, and font scale.
- Align icons.
- Reset icon positions.

Each window stores its own display settings and icon positions.

### Display Effects

- Highlight ready actions.
- Highlight adjusted/transformed actions.
- Show keybind text for skill cooldown windows.
- Show missing auras for aura windows.

### Tracking

- Select tracked skills by job.
- Filter skills by category:
  - Weapon skills
  - Spells
  - Abilities
  - Role actions
- Search skills by name or ID.
- Edit row-based display order.
- Resize the tracked order editor area by dragging the divider.
- Search and add recently seen buffs/debuffs.

## Data Files

### `Data/abilities.json`

This is the main skill metadata file used by the overlay. It contains job, level, action ID, icon ID, display name, category, and charge/cooldown-related metadata used by the plugin.

When a game patch changes actions, this file is the first place to check.

Typical update workflow:

1. Compare the new Korean client action data against the current `abilities.json`.
2. Update changed action IDs, icon IDs, levels, names, categories, and charge data.
3. Check transformed or upgraded actions for affected jobs.
4. Build the plugin.
5. Test in game with the affected jobs and synced level content.

## Code Map

- `Plugin.cs`
  - Plugin entry point, command registration, services, lifecycle.
- `Plugin.ConfigUi.cs`
  - Main settings UI.
- `Plugin.Overlay.cs`
  - Overlay window rendering and icon interaction.
- `Plugin.Cooldowns.cs`
  - Cooldown, charge, action availability, and icon state logic.
- `Plugin.Abilities.cs`
  - Ability loading, filtering, level/job visibility, adjusted action helpers.
- `Plugin.TrackedSkillEditor.cs`
  - Tracked skill selection and display order editor.
- `Plugin.Auras.cs`
  - Buff/debuff tracking UI and aura state handling.
- `Plugin.Keybinds.cs`
  - Hotbar keybind lookup and display text.
- `PluginConfig.cs`
  - Saved configuration model.
- `AbilityDefinition.cs`
  - Ability metadata model.
- `CooldownState.cs`
  - Runtime cooldown display state.
- `AuraState.cs`
  - Runtime aura display state.
- `JobInfo.cs`
  - Job and role helper data.

## Patch Notes for Maintainers

When FFXIV updates actions, check these areas:

- New jobs or removed jobs.
- Action ID changes.
- Icon ID changes.
- Skill name changes in the Korean client.
- Level unlock changes.
- Charge count changes by level.
- Role action availability.
- Skills that upgrade automatically by level.
- Skills that transform through `GetAdjustedActionId`.
- Skills that are only usable with a proc, gauge, stance, or temporary state.

For new jobs, add or verify:

- Job abbreviation.
- Job role.
- All trackable actions in `abilities.json`.
- Role actions.
- Charge skills.
- Upgraded actions.
- Adjusted/transformed actions.
- Keybind and cooldown behavior in game.

## Known Limitations

- This is not a general-purpose public Dalamud plugin distribution yet.
- The project currently uses local absolute Dalamud reference paths.
- Korean action names depend on curated local action data.
- Some job-specific gauge/proc states may still need explicit modeling.
- Aura search is based on currently or recently observed statuses, not a full clean localized status database.
- UI text is mainly Korean.

## Repository Policy

This repository contains a personal custom plugin for experimentation and Korean client use. Contributions and ideas are welcome, but behavior should be tested in game before being treated as stable.

Please do not use this plugin to automate gameplay or bypass game rules. FFXIVAura is intended only as a visual tracking overlay.

## License

No license has been selected yet. Until a license is added, treat the code as all rights reserved by the repository owner.
