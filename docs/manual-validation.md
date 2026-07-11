# Manual Validation Checklist

Use this checklist before pushing a build that changes overlay positioning, auto-alignment, tooltips, or performance profiling.

## Login And Reload

- Log in with at least one skill cooldown window containing manually moved icons.
- Confirm the skill icons keep their saved positions after login finishes.
- Log out to the character select screen, log back in, and confirm the same positions are restored.
- Toggle overlay lock on and off, then confirm locked overlays do not move while tooltips still work.
- Enable `툴팁 표시`, then confirm action tooltips appear above every FFXIVAura window without a tooltip sound or a first-frame position flash.
- Confirm evaluated action descriptions retain maximum charges, full transformed-action lists, potency values, and duration values.
- Disable `툴팁 표시`, then confirm action and aura tooltips no longer appear.

## Level Sync

- Enter synced content where one or more tracked skills are unavailable because of effective level.
- Confirm hidden high-level skills do not leave visible gaps after the first stable frame.
- Leave the synced content or switch to an unsynced area.
- Confirm the full-level layout restores without permanently overwriting the intended saved order.

## Overlay Editing

- Keep settings open, click each unlocked overlay body, and confirm the active window selector and settings content switch to the clicked window.
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
- As Scholar, confirm an effect applied by the local fairy is included when party own-only filtering is enabled.
- Enter combat, let a temporary buff/debuff appear, close the search window, then reopen search after it expires and confirm it can appear as a recent candidate.
- With active-only aura search enabled, search for a status that is not currently visible, click the full-search switch button, and confirm older/recent candidates can appear.
- With active-only aura search enabled, search by the granting skill name for a currently visible status and confirm the active result appears with its skill source tag.
- Search for a status name that has multiple IDs. Confirm normal mode shows one grouped result with an ID-count hint, then enable `ID별 보기` and confirm the individual IDs appear.
- Add a grouped aura, convert it to `이 ID만`, then convert it back with `그룹으로`. Confirm the tracked order and icon position are preserved.
- Search by status name, status ID, action name, and action ID for an action-granted status. Confirm the same result keeps the action source tag.
- Resize the aura search window and confirm the result list expands or shrinks with the window.
- Confirm search results tag active, recently seen, action-granted skill names, and status-list candidates correctly.
- Hover active buff and debuff icons and confirm tooltips appear near the cursor and disappear when leaving the icon.

## Party Cooldown Boards

- Create one Party Defensives window, one Party Healing Cooldowns window, and one Party Damage Synergies window.
- Select the shared `4-player dungeon`, `8-player regular party`, or `24-player alliance` edit mode, then switch between defensive, healing, and synergy windows and confirm every selected window continues editing the same mode.
- With no party formed, switch the shared mode and confirm unlocked party-board overlays move to their corresponding editable positions. Close settings and confirm automatic selection resumes.
- Enter and leave alliance content and confirm that the appropriate layout is restored without overwriting the other layout.
- During alliance HUD reloads and area transitions, confirm that the board does not briefly jump to the regular-party position.
- Join a party and confirm rows follow the in-game party list order.
- Enter a 4-player dungeon and confirm party boards use their 4-player position and size; leave or join an 8-player party and confirm each stored layout returns independently.
- Join 24-player alliance content and confirm rows follow the in-game alliance list order with A/B/C labels on the correct parties.
- Confirm the local party label matches the in-game party-list title (for example, `연합 파티 B` must appear under B).
- Confirm A/B/C are three vertically stacked party sections and each party contains up to eight member rows in party-list slot order.
- With a narrow saved board and large icon/gap settings, confirm the alliance board temporarily expands or scales its rendered icons so every member row remains visible. Leave alliance content and confirm the saved board size is unchanged.
- Confirm each row shows a job icon, the first two characters of the party member name, and the expected ready cooldown icons.
- In settings, uncheck one preset entry and confirm it disappears from the matching party cooldown window only.
- Click the reset button for that window's exclusions and confirm the preset entry appears again.
- Enter synced content and confirm skills above the current effective level disappear.
- Have a party member use a defensive, healer cooldown, or damage synergy skill and confirm the active border appears on that member's row.
- With at least one member wrapping to a second icon line, confirm the job icon/name remains beside the first line and the next member is separated by a visibly larger gap than the wrapped-line gap.
- Compare the caster's native party-list status timer with the board timer. A transient one-second boundary difference is acceptable, but the board must not remain consistently ahead.
- For a skill with separate caster and recipient statuses, such as Divine Veil or Temperance, confirm the board follows the longest still-active related effect instead of switching to a shorter caster-only timer.
- While an active party cooldown is counting down, confirm repeated party-list status samples do not freeze and then drop the board timer by several seconds at once.
- Check a 20-second personal defensive such as Camouflage and confirm mid-duration party-list corrections do not make the board timer skip several seconds at once.
- After the active effect expires, confirm the same icon turns grayscale and shows an estimated cooldown timer.
- Have a Dark Knight use Oblation or a Gunbreaker use Aurora once and confirm the icon shows one remaining charge without turning grayscale. Use the second charge and confirm the icon then enters grayscale cooldown state.
- Confirm a single Oblation or Aurora use observed by both combat log and active status consumes only one displayed charge.
- During a brief party status-list interruption, confirm a returning Oblation or Aurora status does not consume another displayed charge.
- Hover board icons with tooltips enabled and confirm the selected action tooltip mode appears near the cursor.
- Enable the party cooldown log observer, then have a Scholar use fairy actions such as Whispering Dawn, Fey Illumination, or Fey Blessing.
- Confirm the observer records the action as tracked on the Scholar row, not as an ignored pet source. The detail should mention owned summon/object matching when the log source is the fairy.
- If two same-named pets or owned objects are present, confirm ambiguous ownership is ignored instead of starting cooldowns on the wrong party member.
- During duty entry, zone transitions, and party member loading, confirm transient status-list reads do not close the overlay or produce repeated exceptions.
- Confirm a transient status-list read failure does not make existing aura icons or active party-cooldown borders disappear for a frame.
- On a party buff window, watch a short timed mitigation such as Sacred Soil. Confirm its timer decreases smoothly between party-list updates and the icon disappears after expiry even if the party list briefly retains a stale status slot.

## Performance Overlay

- Enable detailed performance profiling.
- Confirm section timings show current, average, max, recent 5-second average, and max time.
- Confirm the window profile lists each visible overlay by window name.
- Reset the profiler and confirm section and window samples clear.
- During combat or a duty pull, watch for repeated spikes above the expected frame budget.
- Confirm frame allocation, average allocation, and Gen0 collection values are shown and written to the CSV frame row.
- With party cooldown boards visible, confirm `statusScansThisFrame` occurs periodically while intervening frames report `statusCacheHitsThisFrame`.
- Confirm `activePartyListSource`, `activePartyListRecipient`, `activeObjectSource`, and `activeObjectRecipient` identify which status source supplied active board timers.
- Confirm cold login/zone frames record `스킬 후보 구성` and `오라 인덱스 구성` separately, and that the work occurs while the overlay is hidden when zone-load hiding is enabled.
- Open aura search, type the same query for several seconds, and confirm `actionAuraQueryCache` and `statusSearchQueryCache` remain bounded, `auraSearchResultCacheHits` increases faster than `auraSearchResultCacheMisses`, and `오라 검색` timing settles after the first query frame.
- Enter and leave level-synced content, then compare the first transition frame with previous profiles. A new effective level should filter the cached job action list instead of rescanning the full Lumina Action sheet.
- If running above 120 FPS, confirm the recent average remains stable instead of changing abruptly from sample capping.
- Enable automatic CSV recording, change several settings, and confirm profile/config queue depth returns to zero with no failed or dropped work.
- Reload the plugin after a queued settings change and confirm the latest settings were persisted.
- Confirm `performance-profile.csv` rotates at the configured limit without a visible frame-time spike.
- Start once with a profile file that has the previous CSV header and confirm it is moved to `performance-profile.previous.csv` before the new schema begins recording.
