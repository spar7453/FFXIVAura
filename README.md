# FFXIVAura

FFXIVAura is a Dalamud-native cooldown and aura tracking overlay for Final Fantasy XIV. It started as a replacement for an ACT/HTML WeakAura-style overlay and is currently focused on the Korean FFXIV client environment.

The goal is simple: show only the skills, buffs, debuffs, charges, cooldowns, and proc states you actually care about, in movable icon windows that can behave like a lightweight WeakAura setup.

> Status: early custom plugin. It is usable for personal testing, but the data model and UI are still evolving.

## Features

- Dalamud-native ImGui overlay, no ACT browser overlay required.
- `/fa` command opens the settings window.
- Multiple overlay windows.
- Per-window options for size, gap, font scale, position, role, and display condition.
- Overlay edit controls for window role, display condition, window name, alignment, and resize.
- Overlay roles:
  - Skill cooldowns
  - Player buffs
  - Target debuffs
  - Party buffs
- Per-job tracked skill lists.
- Per-job icon positions.
- Manual icon dragging inside the overlay grid.
- Drag resize handle for overlay width and height.
- Row-based display order editor.
- Per-window left, center, and right icon alignment.
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
- Party aura options for own-only filtering and party member count display.

## Screenshots

### Overlay Edit Mode

![FFXIVAura overlay edit mode](docs/images/overlay-edit-example.png)

### Overlay / Tracking Settings

![FFXIVAura overlay and tracking settings](docs/images/settings-overlay-tracking.png)

## Configuration Overview

Open the settings window with `/fa`.

### General / Display

- Enable or disable the plugin.
- Lock overlay movement.
- Hide overlays during zone loading.
- Check current job, effective level, loaded action count, and command name.
- Highlight ready actions.
- Highlight adjusted/transformed actions.
- Show keybind text for skill cooldown windows.
- Show missing auras for aura windows.
- Show own-only party auras and party aura count labels.

### Overlay / Tracking

- Add or delete icon windows.
- Change icon size, gap, overlay width, overlay height, and font scale.
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
- Buff/debuff windows set to the Active display condition continuously compact the currently active auras so expired or newly appeared statuses do not leave empty slots.

The most common per-window controls are available directly on the unlocked overlay:

- Top-left buttons switch the window role.
- Top-right display condition combo changes when the window is shown.
- Bottom-left name field renames the window.
- Bottom-right buttons change icon alignment.
- The lower-right corner handle resizes the overlay box.

Each window stores its own display settings and manual icon positions. Buff/debuff windows use automatic compact placement only while their display condition is Active.

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
- `Plugin.OverlayControls.cs`
  - Floating overlay edit controls for role, display condition, name, and alignment.
- `Plugin.OverlayPositions.cs`
  - Shared icon position normalization, resize normalization, and stale position cleanup.
- `Plugin.SkillPositions.cs`
  - Skill icon position storage, auto placement, alignment, and tracked-action key matching.
- `Plugin.AuraPositions.cs`
  - Aura position storage, active-only compact placement, alignment, and stale aura position cleanup.
- `Plugin.OverlayItems.cs`
  - Shared skill/aura item selection for overlay windows.
- `OverlayLayout.cs`
  - Pure overlay layout geometry for auto placement, row alignment, clamping, and overlap checks.
- `OverlayPositionKeys.cs`
  - Shared string key generation and parsing for overlay position maps.
- `RuntimeScopeKeys.cs`
  - Shared string key generation and matching for frame caches, drag state, and runtime window-scoped state.
- `Plugin.Cooldowns.cs`
  - Cooldown, charge, action availability, and icon state logic.
- `CooldownMath.cs`
  - Pure cooldown/charge timing calculations.
- `Plugin.Abilities.cs`
  - Ability loading, filtering, level/job visibility, adjusted action helpers.
- `Plugin.TrackedSkillEditor.cs`
  - Tracked skill selection and display order editor.
- `Plugin.TrackedAbilityState.cs`
  - Tracked skill list mutation, exclusion handling, and order changes.
- `Plugin.AuraTracking.cs`
  - Buff/debuff tracking UI and aura search window.
- `Plugin.AuraSearch.cs`
  - Recently/currently seen aura search and status candidate ordering.
- `Plugin.AuraStates.cs`
  - Player, target, and party aura state calculation.
- `Plugin.AuraRendering.cs`
  - Buff/debuff icon rendering.
- `Plugin.ConfigNormalization.cs`
  - Config migration, default window creation, and saved data normalization.
- `ConfigMapNormalizer.cs`
  - Pure normalization for saved string and position maps.
- `ConfigValueNormalizer.cs`
  - Pure normalization for saved scalar, dimension, and position values.
- `IconWindowIdentity.cs`
  - Overlay window ID, default name, and aura position group remapping helpers.
- `IconWindowClone.cs`
  - Safe deep-copy helpers for cloned overlay window tracking lists and position maps.
- `Plugin.IconWindowRuntimeState.cs`
  - Runtime cache cleanup for added, removed, or normalized overlay windows.
- `Plugin.Keybinds.cs`
  - Hotbar keybind lookup and display text.
- `KeybindTextFormatter.cs`
  - Pure keybind label formatting and unsupported glyph filtering.
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

## Verification

```powershell
dotnet build -c Release --no-restore
dotnet run --project .\tests\FFXIVAura.Tests\FFXIVAura.Tests.csproj -c Release
```

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

- Korean action names depend on curated local action data.
- Some job-specific gauge/proc states may still need explicit modeling.
- Aura search is based on currently or recently observed statuses, not a full clean localized status database.
- UI text is mainly Korean.

## Repository Policy

This repository contains a personal custom plugin for experimentation and Korean client use. Contributions and ideas are welcome, but behavior should be tested in game before being treated as stable.

Please do not use this plugin to automate gameplay or bypass game rules. FFXIVAura is intended only as a visual tracking overlay.

## License

FFXIVAura is released under the MIT License. See [LICENSE](LICENSE).
