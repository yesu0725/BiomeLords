# Changelog

## 0.6.12

Bug fix.

- **The chest/storage window can be dragged to the bottom of the screen again.** It could only be moved around the upper part of the screen — drag it lower and it crept back up as you dragged. The window was being kept inside the wrong rectangle, and it was re-checked on every frame of the drag, so it was pushed back faster than you could pull it down. It is now checked against the actual screen, and only once you let go, so the window goes where you put it and only gets nudged if you genuinely drop it off the edge. If you run Lost Scrolls II as well, this is the same window its companion inventory uses — BiomeLords owns that window's position when both mods are installed, so this fixes the companion inventory too.

**Now requires Jotunn 2.30.1 or newer.** Jotunn 2.30 is the release that was updated for Valheim 1.0, and it brings BiomeLords' admin console commands back. Thunderstore installs it for you; if you manage mods by hand, update Jotunn alongside this release.

## 0.6.11

Valheim 1.0 compatibility.

- **The mod works again on Valheim 1.0.** The 1.0 update changed a number of the game's own methods that BiomeLords hooks into, and the mod was still built against the pre-1.0 game. Four of its hooks could no longer find what they were attaching to and switched themselves off, and several others threw errors every frame — the pedestal ceremony and blessing hover text, the Lord's Horn tooltip, the Featherweight extra rows for crafting output, and the Forsaken Power on-screen messages all stopped working, with errors spamming the log on every respawn. All of it is rebuilt against 1.0 and working again.
- **Lord's Pedestals read their mounted trophy correctly again.** 1.0 changed item stands to store the mounted item as an id rather than a name, which broke the pedestal's trophy lookup — blessings, charge counts and the mount ceremony all keyed off it. Existing pedestals with trophies already mounted are unaffected and pick up where they left off.

**Update Jotunn as well.** Jotunn 2.29.2 cannot register console commands on Valheim 1.0 — that is a Jotunn limitation, not a BiomeLords one, and it affects every mod that uses it. Until Jotunn ships a 1.0-compatible release, BiomeLords' admin console commands are unavailable. Everything else in the mod works.

## 0.6.10

Multiplayer fix.

- **The Neck Lord's summons keep their behaviour in multiplayer.** In 0.6.9 the summons were ordinary Necks adjusted the moment they spawned, and those adjustments only existed on the machine that spawned them — so when a summon's ownership passed to another player mid-fight (which happens routinely as people move around), it quietly reverted to fleeing fire. The Lord now summons a proper creature of its own that carries both traits itself, so they hold no matter who ends up in charge of it, and they survive a zone reload or a relog. It still looks and behaves exactly like a Neck otherwise, and killing one gives no Lord credit.

**Everyone on a server should update together.** This release adds a new creature, and a player still on 0.6.9 will not be able to see the Lord's summons.

## 0.6.9

Neck Lord tuning.

- **The Necks the Neck Lord summons are no longer afraid of fire either.** 0.6.8 freed the Lord itself; its Tide Caller summons were still ordinary Necks and would still scatter from a torch or campfire, which made a lit arena trivial to hold. They now hold their ground the way the Lord does. Necks elsewhere in the world are unchanged — only the ones the Lord calls up.
- **Summoned Necks arrive already hunting you.** They used to spawn idle and drift until they happened to notice a player. They now spawn alert and lock straight onto whoever the Lord is fighting, and they'll keep coming rather than losing interest when you break line of sight.

## 0.6.8

Bug fix.

- **The Neck Lord no longer runs from fire.** It inherited the ordinary Neck's fear of flame, so a torch, campfire or firepit would send it fleeing and make it forget who it was fighting — you could hold a boss off with a torch. The Lord now ignores fire entirely and will walk straight through a lit base to reach you. It takes fire damage the same as before; only the panic is gone.

## 0.6.7

Bug fix.

- **Fixed Featherweight's extra rows still not accepting picked-up items when ComfyQuickSlots is installed.** ComfyQuickSlots replaces Valheim's "find me a free slot" routine with its own, and that routine only ever looks at the first five rows — so with ComfyQuickSlots loaded the Featherweight rows showed up, counted toward your free space, and took hand-dragged items, but auto-pickup, manual pickup and crafting output all reported "inventory full" the moment the rows above them filled. The extra rows are now offered to that routine as well, so every pickup path uses them. Unequipping armour with a full base grid no longer risks stranding the piece either. Installs without ComfyQuickSlots were unaffected and are unchanged.

## 0.6.6

Bug fix.

- **Fixed Featherweight's extra rows not accepting picked-up items.** The two extra rows were visible and you could drag items into them by hand, but the game didn't count them as real space — so with your normal slots full you'd get "inventory full" and nothing would be auto-picked up, even with the extra rows completely empty. They now behave like any other inventory row: auto-pickup, manual pickup, crafting output and a container's "take all" all fill them.

## 0.6.5

Quality-of-life and balance.

- **The chest/storage window can now be dragged anywhere on screen.** Click and hold anywhere on an open storage window that isn't an item slot — the backdrop, the header, the weight readout — and drag it wherever you want. Item slots keep working exactly as before, so moving items around is unaffected. Where you drop it is remembered when you reopen the chest and after you log back in.
- **New settings for the storage window position.** `UI.StorageUiOffsetColumns` and `UI.StorageUiOffsetRows` place the window numerically, measured in inventory cells so it looks the same at any UI scale. By default the window now sits two rows below the player inventory, clear of the Featherweight blessing's extra rows. These two settings are **local to each player** — a server never overwrites them, so everyone keeps their own layout.
- **Removed the automatic ComfyQuickSlots detection for chest window placement.** The window used to be shifted automatically based on which inventory mods were installed. It's now positioned purely by your own setting or your own drag, which behaves the same way no matter what else you have loaded. ComfyQuickSlots remains fully compatible in every other respect.
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
