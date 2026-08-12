# Changelog

## 0.6.5

Quality-of-life and balance.

- **The chest/storage window can now be dragged anywhere on screen.** Click and hold anywhere on an open storage window that isn't an item slot — the backdrop, the header, the weight readout — and drag it wherever you want. Item slots keep working exactly as before, so moving items around is unaffected. Where you drop it is remembered when you reopen the chest and after you log back in.
- **New settings for the storage window position.** `UI.StorageUiOffsetColumns` and `UI.StorageUiOffsetRows` place the window numerically, measured in inventory cells so it looks the same at any UI scale. By default the window now sits two rows below the player inventory, clear of the Featherweight blessing's extra rows. These two settings are **local to each player** — a server never overwrites them, so everyone keeps their own layout.
- **Removed the automatic ComfyQuickSlots detection for chest window placement.** The window used to be shifted automatically based on which inventory mods were installed. It's now positioned purely by your own setting or your own drag, which behaves the same way no matter what else you have loaded. ComfyQuickSlots remains fully compatible in every other respect.
- **Fixed Featherweight's extra rows not accepting picked-up items.** The two extra rows were visible and you could drag items into them, but the game didn't count them as real space — so with your normal slots full you'd get "inventory full" and items wouldn't be auto-picked up, even with the extra rows completely empty. They now behave like any other inventory row: auto-pickup, manual pickup, crafting output and container "take all" all fill them.
- **Forest's Embrace grants Rested faster.** The time you need to stay seated beside a mature tree dropped from **60 seconds to 30**. Everything else about the blessing is unchanged.

## 0.6.4

Config addition.

- **Lord's Horn crafting recipe is now admin-configurable.** A new `[LordsHorn] Recipe` server-synced config entry lets admins view and change the Workbench requirements for crafting the Lord's Horn (default `NeckTail:5,TrophyDeer:1,Bronze:1`), using the same comma-separated `ItemPrefab:Amount` format as the Hall of the Lords recipe. Takes effect on restart.

## 0.6.3

Greydwarf Shaman Lord rework — blessing and Forsaken Power.

- **Blessing renamed to Forest's Embrace.** The Quick Sprout blessing (faster crop growth) now also carries the old Forest's Embrace tree-rest effect: sit beside a mature tree with no monsters nearby to heal over time and earn an extended Rested buff, scaled by the tree's age. Both effects are active together as long as the blessing is held.
- **New Forsaken Power: Rootward.** Replaces the old Forest's Embrace power. On activation it heals you for 100 HP and erupts a protective TentaRoot on each of your closest enemies — up to 5. The roots fight on your side and can never harm you, retracting once their work is done. Existing players with the old Forsaken Power are upgraded automatically.

## 0.6.2

Compatibility fix.

- **Fixed a stretched/broken inventory panel with Shudnal ExtraSlots or AzuExtendedPlayerInventory installed.** Both of those mods grow the player inventory and manage the extra rows themselves. BiomeLords now detects either one and stops adding its own Featherweight rows (and stops resizing the inventory window), which previously left a large empty area below your slots. The Featherweight blessing's raised carry-weight cap still works normally — only the extra-row part is disabled when one of these mods is present. ComfyQuickSlots is unaffected and remains fully compatible.

## 0.6.1

Compatibility fix.

- **Fixed a crash and item-loss bug with ComfyQuickSlots.** The Fallen Valkyrie Lord's Featherweight blessing (extra inventory rows) now correctly accounts for ComfyQuickSlots' 5-row player inventory layout: no more inventory-open crash, equipped armor is no longer swept into a crate when switching blessings, and Featherweight's extra-row items now survive logout/login while ComfyQuickSlots is installed.

## 0.6.0

Initial public release.

- **Seven Biome Lords**, one per biome from Meadows through Ashlands, each with its own look, attacks, and personality.
- **Lord's Horn** — a craftable relic that summons the Lord matching your current biome, once you've hunted enough of that biome's creatures and night has fallen.
- **Lord's Pedestal** — mount a Lord's trophy to receive its Blessing, a permanent passive perk that lasts as long as the trophy is on display. Place several pedestals together to build your own Hall of the Lords.
- **Seven Blessings** — better fishing luck, faster crop growth, extra ore from mining, a loyal and faster-breeding wolf pack, longer-lasting food buffs, bonus crafting output, and a much higher carry limit.
- **Seven Forsaken Powers** — earned automatically by landing the killing blow on a Lord. Each one offers a different way to turn a fight, from restoring your party in an instant to revealing every nearby threat on your map.
- **Difficulty that follows you** — Lords scale up in toughness as you defeat more of the game's bosses, so they stay a real challenge for veteran players without overwhelming newer ones.
- **No new art assets** — every Lord, item, and effect is built entirely from Valheim's own creatures and visuals, so it fits right in.
