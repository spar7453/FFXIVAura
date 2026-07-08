# Manual Validation Checklist

Use this checklist before pushing a build that changes overlay positioning, auto-alignment, tooltips, or performance profiling.

## Login And Reload

- Log in with at least one skill cooldown window containing manually moved icons.
- Confirm the skill icons keep their saved positions after login finishes.
- Log out to the character select screen, log back in, and confirm the same positions are restored.
- Toggle overlay lock on and off, then confirm locked overlays do not move while tooltips still work.

## Level Sync

- Enter synced content where one or more tracked skills are unavailable because of effective level.
- Confirm hidden high-level skills do not leave visible gaps after the first stable frame.
- Leave the synced content or switch to an unsynced area.
- Confirm the full-level layout restores without permanently overwriting the intended saved order.

## Overlay Editing

- Add a second overlay window, rename it, change its role, and delete it.
- Confirm deleting an overlay window opens a confirmation popup, and cancel leaves the window unchanged.
- Add another window and confirm the deleted window number is reused when available.
- Add a default window and confirm it starts as a clean skill cooldown window, then clone the current window and confirm the clone copies its visible setup.
- Set different display conditions for a skill role and an aura role, switch roles, and confirm each role restores its own previous condition.
- Resize each overlay from the bottom-right handle.
- Confirm skill windows keep row structure after resize and aura windows compact active icons correctly.
- In edit mode, Ctrl + right-click a skill icon and confirm it is removed from that overlay. Confirm plain right-click does not remove it.

## Aura Windows

- Track a player buff, target debuff, and party buff.
- Switch display condition to active only and confirm active aura icons auto-compact without row gaps.
- Manually place aura icons in a non-active display condition, switch to active only, then switch back and confirm the manual positions are preserved.
- Toggle party own-only filtering and confirm search/current candidates match the filter.
- Enter combat, let a temporary buff/debuff appear, close the search window, then reopen search after it expires and confirm it can appear as a recent candidate.
- With active-only aura search enabled, search for a status that is not currently visible, click the full-search switch button, and confirm older/recent candidates can appear.
- With active-only aura search enabled, search by the granting skill name for a currently visible status and confirm the active result appears with its skill source tag.
- Search for a status name that has multiple IDs and confirm same-name results show distinct IDs and a same-name hint.
- Search by status name, status ID, action name, and action ID for an action-granted status. Confirm the same result keeps the action source tag.
- Resize the aura search window and confirm the result list expands or shrinks with the window.
- Confirm search results tag active, recently seen, action-granted skill names, and status-list candidates correctly.
- Hover active buff and debuff icons and confirm tooltips appear near the cursor and disappear when leaving the icon.

## Party Cooldown Boards

- Create one Party Defensives window, one Party Healing Cooldowns window, and one Party Damage Synergies window.
- Join a party and confirm rows follow the in-game party list order.
- Confirm each row shows a job icon, the first two characters of the party member name, and the expected ready cooldown icons.
- In settings, uncheck one preset entry and confirm it disappears from the matching party cooldown window only.
- Click the reset button for that window's exclusions and confirm the preset entry appears again.
- Enter synced content and confirm skills above the current effective level disappear.
- Have a party member use a defensive, healer cooldown, or damage synergy skill and confirm the active border appears on that member's row.
- After the active effect expires, confirm the same icon turns grayscale and shows an estimated cooldown timer.
- Hover board icons with tooltips enabled and confirm the game action tooltip appears near the cursor.
- Enable the party cooldown log observer, then have a Scholar use fairy actions such as Whispering Dawn, Fey Illumination, or Fey Blessing.
- Confirm the observer records the action as tracked on the Scholar row, not as an ignored pet source. The detail should mention owned summon/object matching when the log source is the fairy.
- If two same-named pets or owned objects are present, confirm ambiguous ownership is ignored instead of starting cooldowns on the wrong party member.

## Performance Overlay

- Enable detailed performance profiling.
- Confirm section timings show current, average, max, recent 5-second average, and max time.
- Confirm the window profile lists each visible overlay by window name.
- Reset the profiler and confirm section and window samples clear.
- During combat or a duty pull, watch for repeated spikes above the expected frame budget.
- If running above 120 FPS, confirm the recent average remains stable instead of changing abruptly from sample capping.
