# BiomeLords

Every biome hides a Lord — a towering, named version of one of its creatures, waiting for a worthy hunter to call it out.

## What this mod adds

- **One Lord per biome.** Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, and the Deep North each have their own Lord, scaled up and far tougher than anything else that biome throws at you.
- **A relic that summons them.** Craft the **Lord's Horn**, hunt enough of that biome's regular creatures to prove yourself, then use the Horn at night in that biome to call the Lord out.
- **A trophy worth keeping.** Defeat a Lord and it drops a unique trophy. Mount it on a **Lord's Pedestal** to receive that Lord's **Blessing** — a permanent passive perk for as long as the trophy stays on display.
- **A power you carry into battle.** Landing the killing blow on a Lord also grants its **Forsaken Power**, usable like any other Forsaken Power from a fallen boss.
- **A fight that grows with you.** Lords scale up in toughness based on how many Lords you've already defeated, so even an early-game Lord can still be a real fight for a veteran — without one-shotting a newcomer.
- **A storage window you can put where you want.** Click and drag any open chest window anywhere on screen; where you drop it is remembered between sessions. Configurable numerically too, per player.

## Why hunt them

Each Lord offers its own flavor of reward — some make daily life easier (better fishing, longer-lasting food, faster crops, bonus crafting output), some make you tougher to fight (less weight, more carrying capacity), and their Forsaken Powers each open up a different way to turn the tide of a fight. Full details on where to find each Lord, what their Blessings and Powers do, and how the scaling works are in the **Wiki**.

## Getting started

1. Craft a Lord's Horn.
2. Go hunt the regular creatures of a biome until you've proven yourself.
3. Return to that biome at night and use the Horn.
4. Survive the fight, claim the trophy and the Power, and bring the trophy home to a Lord's Pedestal.

See the [Wiki](https://github.com/yesu0725/BiomeLords/wiki) for the full hunter's guide.

---

## Compatibility

BiomeLords is built **entirely from vanilla Valheim assets** — every Lord, item, and effect is a cloned/retinted vanilla prefab, with no custom models or asset bundles. This keeps the footprint small and avoids clashing with most other mods.

Known interactions with other mods:

- **[ComfyQuickSlots](https://thunderstore.io/c/valheim/p/Cumfy/ComfyQuickSlots/)** — fully compatible as of **v0.6.1**, with a further fix in **v0.6.7**. ComfyQuickSlots expands the player inventory by a 5th row for armor + quickslot bindings; BiomeLords' Featherweight blessing (Fallen Valkyrie Lord) detects that layout and adapts its own extra-row math around it. Before 0.6.1, having both mods installed could crash on opening the inventory, spill equipped armor into a crate when switching blessings, or lose Featherweight's extra-row items on logout. Before 0.6.7, ComfyQuickSlots' own "find a free slot" routine only looked at the first five rows, so with both mods installed the Featherweight rows showed up and took hand-dragged items but auto-pickup, manual pickup and crafting output all reported "inventory full" — all fixed.
- **Chest/storage window placement** — as of **v0.6.5** this is no longer tied to mod detection at all. The window sits wherever you drag it (or wherever the `UI.StorageUiOffsetColumns` / `StorageUiOffsetRows` settings put it), and behaves identically with or without ComfyQuickSlots installed. Those two settings are local to each player and are never overwritten by a server.
- **[Shudnal ExtraSlots](https://thunderstore.io/c/valheim/p/Shudnal/ExtraSlots/)** and **[AzuExtendedPlayerInventory](https://thunderstore.io/c/valheim/p/Azumatt/AzuExtendedPlayerInventory/)** — as of **v0.6.2**, detected automatically. Both of these mods grow the player inventory and manage that space themselves, so Featherweight no longer tries to add its own rows (or resize the inventory window) when either is installed — you still get the blessing's raised carry-weight cap, just without the extra-row buff, since these mods already provide extra slots of their own.
- **Other inventory/UI mods** that resize, rename, or otherwise hook the player inventory grid haven't all been tested. If you find a conflict, please [open an issue](https://github.com/yesu0725/BiomeLords/issues) with your modlist and the error log (`LogOutput.log`) so it can be diagnosed.
- **Valheim 1.0's purchasable inventory rows** — rows bought from Haldor are treated as part of your normal inventory, and the Featherweight blessing's extra rows stack **below** them, so the two add up and buying a row while blessed disturbs nothing you're carrying. Fixed in **v0.6.14**: before that, the game's own row reset dropped the Featherweight rows on the ground on every login and on every purchase, and BiomeLords could shrink a bought inventory back to four rows and crate what was in the rows you'd paid for. Update if you have bought rows.
- **Server-authoritative config** — `BiomeLords` requires every client to have the mod (`EveryoneMustHaveMod`) and matching major.minor version; admin-set config values are pushed from the server to clients automatically. The one exception is the `UI` section (storage window placement), which is deliberately client-side so each player keeps their own layout.
- **Joining a server without BiomeLords** — you are refused at the connect screen, with Jötunn's mod-compatibility window in place of the usual error box (BiomeLords appears under *Additional Mods Loaded*). That's the `EveryoneMustHaveMod` setting doing its job: you never spawn in, and neither your character nor that server's world is touched. Use a profile without BiomeLords to play on such a server.
- **Keep a server and its players on the same version.** Because only major.minor is enforced, two different `0.6.x` builds can connect to each other — but a release that adds a creature or item leaves older clients unable to see it. A player still on 0.6.9 joining a 0.6.10 server, for example, cannot see the Neck Lord's summons at all.

## Try it out

This mod was built for the **TaegukGaming community server**. If you want to see it in action alongside a curated modpack, check out:

🏰 **[Hearthbound Valheim Modpack](https://thunderstore.io/c/valheim/p/TaegukGaming/Hearthbound_Valheim_Modpack/)**

## Disclaimer

This mod is **created using AI**. No other mods were copied during the process. All feature ideas come from the uploader and are mainly to cater the needs of the **TaegukGaming community server**. If any features or ideas look similar to other mods, these are not intentional.

This mod is **free to use as is**. Voluntary support is appreciated.

---

**Source / issues / wiki:** https://github.com/yesu0725/BiomeLords
