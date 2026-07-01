# BiomeLords

Every biome hides a Lord — a towering, named version of one of its creatures, waiting for a worthy hunter to call it out.

## What this mod adds

- **One Lord per biome.** Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, and Ashlands each have their own Lord, scaled up and far tougher than anything else that biome throws at you.
- **A relic that summons them.** Craft the **Lord's Horn**, hunt enough of that biome's regular creatures to prove yourself, then use the Horn at night in that biome to call the Lord out.
- **A trophy worth keeping.** Defeat a Lord and it drops a unique trophy. Mount it on a **Lord's Pedestal** to receive that Lord's **Blessing** — a permanent passive perk for as long as the trophy stays on display.
- **A power you carry into battle.** Landing the killing blow on a Lord also grants its **Forsaken Power**, usable like any other Forsaken Power from a fallen boss.
- **A fight that grows with you.** Lords scale up in toughness based on how far you've progressed through the game's bosses, so even an early-game Lord can still be a real fight for a veteran — without one-shotting a newcomer.

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

- **[ComfyQuickSlots](https://thunderstore.io/c/valheim/p/Cumfy/ComfyQuickSlots/)** — fully compatible as of **v0.6.1**. ComfyQuickSlots expands the player inventory by a 5th row for armor + quickslot bindings; BiomeLords' Featherweight blessing (Fallen Valkyrie Lord) now detects that layout and adapts its own extra-row math around it. Before 0.6.1, having both mods installed could crash on opening the inventory, spill equipped armor into a crate when switching blessings, or lose Featherweight's extra-row items on logout — all fixed.
- **[Shudnal ExtraSlots](https://thunderstore.io/c/valheim/p/Shudnal/ExtraSlots/)** and **[AzuExtendedPlayerInventory](https://thunderstore.io/c/valheim/p/Azumatt/AzuExtendedPlayerInventory/)** — as of **v0.6.2**, detected automatically. Both of these mods grow the player inventory and manage that space themselves, so Featherweight no longer tries to add its own rows (or resize the inventory window) when either is installed — you still get the blessing's raised carry-weight cap, just without the extra-row buff, since these mods already provide extra slots of their own.
- **Other inventory/UI mods** that resize, rename, or otherwise hook the player inventory grid haven't all been tested. If you find a conflict, please [open an issue](https://github.com/yesu0725/BiomeLords/issues) with your modlist and the error log (`LogOutput.log`) so it can be diagnosed.
- **Server-authoritative config** — `BiomeLords` requires every client to have the mod (`EveryoneMustHaveMod`) and matching major.minor version; admin-set config values are pushed from the server to clients automatically.

## Try it out

This mod was built for the **TaegukGaming community server**. If you want to see it in action alongside a curated modpack, check out:

🏰 **[Hearthbound Valheim Modpack](https://thunderstore.io/c/valheim/p/TaegukGaming/Hearthbound_Valheim_Modpack/)**

## Disclaimer

This mod is **created using AI**. No other mods were copied during the process. All feature ideas come from the uploader and are mainly to cater the needs of the **TaegukGaming community server**. If any features or ideas look similar to other mods, these are not intentional.

This mod is **free to use as is**. Voluntary support is appreciated.

---

**Source / issues / wiki:** https://github.com/yesu0725/BiomeLords
