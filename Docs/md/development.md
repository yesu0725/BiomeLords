# BiomeLords — Development Guide

See also: [design-rules.md](design-rules.md), [patches.md](patches.md)

---

## Build

```powershell
# From the project root
dotnet build -c Release
```

Output: `bin\Release\netstandard2.1\BiomeLords.dll`

Warnings are expected (Jotunn and Valheim assembly nullability); errors are not.
The build should always end with `0 Error(s)`.

### Reference assemblies

Two roots, set in `BiomeLords.csproj`:

| Property | Points at | Supplies |
|---|---|---|
| `ValheimPath` | Steam client install | `assembly_valheim`, `assembly_utils`, `assembly_guiutils`, all `UnityEngine.*` |
| `GaleProfilePath` | Gale profile **TG Mods Only** | `BepInEx`, `0Harmony`, `Jotunn` |

They are split because **Valheim 1.0 ships no `BepInEx\` folder inside the Steam install**
— Gale keeps its own copy per profile — so the game assemblies and the modding libraries
live in different places. If a build fails with `Could not locate the assembly "BepInEx"`
(or `0Harmony` / `Jotunn`), `GaleProfilePath` is pointing at a profile that doesn't exist
or doesn't have Jotunn installed. Point it at whichever profile holds the Jotunn version
you want to compile against.

---

## Deploy

The `CopyToPlugins` target in `BiomeLords.csproj` runs after every build and copies the
DLL to **one** destination — the Gale client profile **HB Test**:

| Destination | Path |
|---|---|
| Gale client profile **HB Test** (the only deploy target) | `%APPDATA%\com.kesomannen.gale\valheim\profiles\HB Test\BepInEx\plugins\TaegukGaming-BiomeLords` |

**No other profile receives builds** — not TG Mods Only, not the dedicated server, not
r2modman. That is deliberate (decided 2026-09-18): those profiles hold whatever build was
last put there on purpose, so a test build can never leak into a profile someone plays on.
If a build is ever needed elsewhere, copy it by hand.

(`GaleProfilePath` — TG Mods Only — is still used for *reference assemblies* at compile
time; that is unrelated to deployment.)

The old first hop, `$(ValheimPath)\BepInEx\plugins\BiomeLords`, was dropped with 0.6.11:
that folder no longer exists (see *Reference assemblies* above) and creating it would plant
a stray `BepInEx\` in a vanilla install. The dedicated-server hop was dropped with 0.6.13
along with TG Mods Only; it also used to fail with `MSB3021 … user-mapped section open`
whenever the server was running.

To deploy by hand:

```powershell
$src  = "E:\Valheim Modding\ValheimBiomeLords\Github\BiomeLords\bin\Release\netstandard2.1\BiomeLords.dll"
$dest = "C:\Users\yesu0725\AppData\Roaming\com.kesomannen.gale\valheim\profiles\HB Test\BepInEx\plugins\TaegukGaming-BiomeLords\BiomeLords.dll"
Copy-Item $src $dest -Force
```

Confirm the deployed copy is the build you think it is — a stale DLL is the classic
"but I fixed that" trap:

```powershell
Get-FileHash bin\Release\netstandard2.1\BiomeLords.dll, "$dest" | Format-Table Hash, Path
```

---

## Logs

BepInEx log for runtime errors (per Gale profile — substitute the profile you launched):
```
C:\Users\yesu0725\AppData\Roaming\com.kesomannen.gale\valheim\profiles\HB Test\BepInEx\LogOutput.log
```

Key lines to look for after a fresh launch:
```
[Info   :BiomeLords] BiomeLords 0.6.13 loaded. 7 Lords registered.
[Info   :BiomeLords] Harmony: N patch classes applied, M skipped.
```

If `M > 0`, check for `[Error :BiomeLords] Patch class ... failed:` lines.
Common causes: vanilla method renamed in a Valheim update, ambiguous overload,
wrong parameter name in postfix signature. See *Surviving a Valheim update* below —
each of those has a distinct log signature and a distinct fix.

A `MissingMethodException: Method not found: ... ` in a stack trace that passes through
BiomeLords code is **not** a patch problem: it means a vanilla method the mod *calls*
changed signature and the DLL was compiled against the old one. Rebuild.

---

## Surviving a Valheim update

Valheim 1.0 (September 2026) broke the mod without changing a line of its code — six
vanilla members changed shape. This is the procedure that found and fixed all of them
in one pass; do it whenever the game updates, **before** launching.

**1. Decompile the new assembly.** `ilspycmd` is installed as a global dotnet tool.

```powershell
$out = "$env:TEMP\vh_decomp"
New-Item -ItemType Directory -Force $out | Out-Null
ilspycmd -p -o $out "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll"
```

(`-p` writes one `.cs` per type, which is what the checker below expects. `-t TypeName`
decompiles a single type when you only need to read one thing.)

**2. Run the patch checker.** It verifies, offline, every Harmony target and every injected
parameter name against the decompile — the two things the C# compiler cannot see:

```powershell
python Docs/tools/verify_patch_targets.py --decompiled $out
```

Exit code 0 means every `[HarmonyPatch(typeof(T), "name")]` / `AccessTools` lookup still
resolves and every `Prefix`/`Postfix` parameter name still matches vanilla. Anything it
prints is a load-time failure you have just avoided.

**3. Build.** The compiler catches the rest: changed return types (`GetAttachedItem` going
`string` → `int` in 1.0), changed parameter types, removed members.

**4. Read the log once.** Two things slip past both steps above and only show in
`LogOutput.log`:

| Log line | Meaning | Fix |
|---|---|---|
| `AccessTools.DeclaredMethod: Could not find method for type X and name Y and parameters (…)` | A patch's explicit `new[] { typeof(...) }` array no longer matches — vanilla added or changed a parameter | Extend the type array to the new signature (`HornTooltipPatch`, `FeatherweightCapacityPatch` in 1.0) |
| `Ambiguous match for HarmonyMethod[(class=X, methodname=Y, … args=undefined)]` | Vanilla added an overload and the patch names the method without an argument list | Give it one, or switch to `TargetMethods()` and patch every overload (`InventoryExpandLoadPatch`) |
| `Failed to patch … Parameter "x" not found in method …` → `IL Compile Error` | A `Prefix`/`Postfix` parameter is named after a vanilla parameter that was renamed | Rename to match; if the *type* changed too, resolve the new value back (`ItemStand_SetVisualItem_MountHook`: `string itemName` → `int itemHash`) |
| `MissingMethodException: Method not found: … (A,B,C)` at runtime, stack through BiomeLords | A method the mod **calls** gained a parameter (even an optional one — C# bakes the full argument list into the call site) | Just rebuild against the new assembly; nothing to edit |

**What changed in 1.0**, for reference:

| Vanilla member | Change | Where it bit |
|---|---|---|
| `ItemDrop.ItemData.GetTooltip(...)` | `+ bool appending` | `HornTooltipPatch` |
| `Inventory.Load(ZPackage)` | second overload `Load(ZPackage, bool)` added | `InventoryExpandLoadPatch` |
| `Inventory.AddItem(string, …, Vector2i, bool)` | `+ bool pickedUp, bool dropIfFullInv` | `FeatherweightCapacityPatch` |
| `ItemStand.SetVisualItem(string itemName, int, int)` | `(int itemHash, int, int, int orientation)` | `PedestalInteractPatch` |
| `ItemStand.GetAttachedItem()` | returns `int` hash, was `string` name | `PedestalInteractPatch`, `BlessingSystem.ResolveAttachedName` |
| `SEMan.AddStatusEffect(...)` | `+ short variant` | every blessing/power grant (rebuild) |
| `Character.Message(...)` | `+ bool log` | `PowerEffectsService` and others (rebuild) |
| `Terminal.ConsoleCommand` ctor | `+ bool onlyAdmin` (13 args) | **Jotunn**, not us — see below |

**Jotunn.** Jotunn 2.29.2's `CommandManager` looks up a 12-argument `ConsoleCommand`
constructor by exact type array and logs `No suitable constructor for Terminal.ConsoleCommand
found` once per command on 1.0. Every `DebugCommands` entry is silently dropped; nothing else
in Jotunn was observed to break. It is Jotunn's bug — update Jotunn when a 1.0-compatible
release exists, and bump the `ValheimModding-Jotunn-x.y.z` dependency string in
`Thunderstore files/BiomeLords/manifest.json` to match.

---

## Key config entries

All **gameplay** config entries are admin-only and server-synced via Jotunn. Found in
`BiomeLords.cfg` (generated by BepInEx on first launch).

The `UI` section is the deliberate exception — bound **without** `IsAdminOnly` so it stays
client-side. It's pure window placement, rewritten whenever the player drags the storage
window, so server sync would fight the player. See
[systems.md § Storage window placement](systems.md#storage-window-placement).

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `General` | `GlobalLordDefeats` | `false` | When `false`, each player's Lord defeat progression is tracked independently via player unique keys. When `true`, any Lord kill on the server advances scaling for every player via world global keys. |
| `General` | `EnableKillTracking` | `true` | Master switch for kill tracking. Disable to freeze counters without losing existing data. |
| `General` | `DebugLogging` | `false` | Verbose scaling + kill tracking logs. Spammy — leave off in normal play. |
| `LordStats.BaseHealth` | `<lordId>` | boss HP (500 … 47000) | Per-Lord base HP at its native tier, before progression scaling. Defaults to the biome boss's HP (Neck Lord 500 … Gammeltroll Lord 47000); `<= 0` falls back to the default. |
| `LordStats.HealthMultiplier` | `<lordId>` | `1.0` | Per-Lord HP multiplier on top of the scaled base HP. |
| `LordStats.DamageMultiplier` | `<lordId>` | `1.0` | Per-Lord damage multiplier on top of the resolved attack profile (× intrinsic). |
| `KillRequirements` | `<lordId>` | varies | Kills needed before the Horn can summon that Lord. |
| `Blessings` | `FimbulHideSnowShedRadius` | `30` | Fimbul Hide: radius in which building pieces shed snow buildup (Deep North only). 0 disables shedding. |
| `ForsakenPowers` | `PetrifyDuration` | `6` | Petrify: seconds the stone skin lasts before it shatters. |
| `ForsakenPowers` | `PetrifyShatterRadius` | `8` | Petrify: shatter burst radius in metres. |
| `ForsakenPowers` | `PetrifyShatterDamage` | `80` | Petrify: frost damage dealt by the shatter. |
| `LordsHorn` | `Recipe` | `NeckTail:5,TrophyDeer:1,Bronze:1` | Comma-separated `ItemPrefab:Amount` pairs for crafting the Lord's Horn at the Workbench. Restart required to take effect. |
| `Hall` | `Recipe` | `Stone:40,FineWood:20,Flint:10,SurtlingCore:3` | Comma-separated `ItemPrefab:Amount` pairs for building the Hall of the Lords. Restart required to take effect. |
| `UI` *(local)* | `StorageUiOffsetColumns` | `0` | Chest/storage window horizontal offset from its vanilla spot, in inventory cell widths (+ = right). Rewritten on drag. |
| `UI` *(local)* | `StorageUiOffsetRows` | `2` | Chest/storage window vertical offset from its vanilla spot, in inventory row heights (+ = down). Rewritten on drag. |

---

## Admin console commands

Enable the console in Valheim: `F5` (developer mode required).

### `biomelords_intrinsic`

Live-tune a Lord's baked damage intrinsic multiplier for the current session. (Defaults
are all `1.0` now — Lord damage comes from the matched attack profile, so this is a
balance-experiment knob layered on top.)

```
# List all Lords with current and default values (* = changed from default)
biomelords_intrinsic

# Set fenring_lord intrinsic to 0.40
biomelords_intrinsic fenring_lord 0.40

# Reset one Lord to its baked default
biomelords_intrinsic fenring_lord reset

# Reset all Lords to baked defaults
biomelords_intrinsic reset
```

Changes take effect immediately — `RefreshLordInstances` re-resolves the attack profile
and rewrites `LordProfileRegistry` + `LordDamageRegistry` for all live instances of that
Lord in the scene.

Session-only: next game launch starts from the baked defaults (all `1.0`) in `LordIntrinsic.cs`.

### `biomelords_tame_time [radius]`

Lists every untamed tameable creature within `radius` (default 10 m) of the admin, showing
name, distance, remaining tame time (`Xm Ys`), percent tamed, and total tame time. Reads
`ZDOVars.s_tameTimeLeft` directly off each creature's `ZNetView`/`ZDO`. Primarily used to
verify Pack Whisperer's taming-speed patch (`Tameable_DecreaseRemainingTime_PackWhisperer`
in [patches.md](patches.md)) is actually halving remaining time over real time.

```
# Default 10 m radius
biomelords_tame_time

# Custom radius
biomelords_tame_time 20
```

---

## Testing checklist

### Before first in-game test of any new feature

- [ ] Build succeeds with 0 errors
- [ ] DLL deployed to the Gale **HB Test** profile (automatic on build)
- [ ] LogOutput.log shows all 8 Lords registered and 0 patch errors

### Lord-defeat scaling

| Test | What to verify |
|------|----------------|
| First Lord kill (per-player mode) | BepInEx log shows `Recorded Lord defeat: <id> (player key — <name> only)` |
| First Lord kill (global mode) | Log shows `(global key — all players affected)` |
| Summon lower-tier Lord after killing higher-tier Lord | Summoned Lord's HP converges to `TierTable.HpFor(highestDefeatedTier)`; its attack adopts that tier's target magnitude (e.g. Neck Lord after tier-3 → 58 slash, up from its native 6) |
| Vanilla boss spawn after Lord kill | Log shows `Vanilla boss scaled: <prefab> ... effectiveTier=N ... (magnitude X→Y)` |
| Vanilla boss HP | Boss bar health matches `TierTable.HpFor(effectiveTier)` |
| Vanilla boss damage | Hits harder, but keeps its own damage/elemental types; magnitude-ratio mult logged |
| No Lords killed | Vanilla bosses and Lords spawn at native tier — no log line from `VanillaBossScalingPatch` |
| Switch `GlobalLordDefeats` to `true` | Second player on server sees updated scaling after first player kills a Lord |

### Per-Lord test items

Use `devcommands` in console to spawn Lords and test items:

```
# Spawn a Lord directly
spawn NeckLord 1

# Give Lord's Horn
give LordsHorn 1

# Give a trophy (for pedestal testing)
give TrophyNeckLord 1

# Check kill counts
biomelords_intrinsic  (lists active tuning state)
```

| Test | What to verify |
|---|---|
| Kill gating | Horn tooltip shows kill count; Horn fails below threshold |
| Summon | Correct Lord spawns in correct biome; world event starts |
| Boss bar | Shows "$enemy_*lord" name |
| Drops | Trophy + biome resources drop on death |
| FP grant | Forsaken Power granted automatically on kill |
| FP HUD | FP icon + tooltip appear in active effects |
| FP cooldown | 20 min cooldown (1200 s) between activations |
| Blessing | Pedestal + trophy → SE_*Spirit applied; HUD icon visible |
| Blessing compendium | Active effects panel shows blessing name + tooltip |
| Blessing exclusivity | Applying a new blessing removes the previous one |
| Blessing persistence | Active blessing survives logout & death (re-applied on spawn) |
| Pedestal charges | 5 uses per trophy; trophy destroyed on 0 charges |

### Neck Lord fire behaviour and summons

- [ ] **Lord ignores fire** — stand behind a lit campfire or hold a torch: the Neck Lord walks straight through and keeps attacking. It must not flee, and must not lose its target (`m_afraidOfFire` nulls `m_targetCreature`, so a break-off means the flag is back)
- [ ] **Lord still takes fire damage** — only the panic was removed; a fire arrow / firepit still hurts it
- [ ] **Summons ignore fire** — same test against the Necks the Lord calls up (Tide Caller, every 45 s)
- [ ] **Summons arrive hunting** — a fresh summon is alert and moving toward the player on its first moments, not idling; it keeps coming after you break line of sight behind terrain
- [ ] **Summons are `NeckLordMinion`** — with `DebugLogging` on, the startup log shows `Registered creature: NeckLordMinion`. In-game the summons still read as "Neck" (name, model, drops)
- [ ] **Summons grant no Lord credit** — killing one gives **no** Forsaken Power, does not end the world event, does not reset kill counters, and it staggers normally (it must not inherit the Lord's stagger immunity)
- [ ] **Ownership transfer (multiplayer, needs 2 clients)** — the 0.6.10 regression test. Have player A trigger the summons, then walk away so ownership passes to player B. The summons must still ignore fire under B's control. Before 0.6.10 they silently reverted, since the AI writes only existed on A's machine
- [ ] **Vanilla Necks unchanged** — an ordinary wild Neck elsewhere in the world still flees from a torch

### Featherweight (Fallen Valkyrie Lord) specific

- [ ] **No penalty under cap** — load between base capacity and 1000: walk/run at full speed, stamina regenerates, no encumbered animation, dodge works
- [ ] **Cap engages** — at 1000 weight the normal encumbered state applies (crouch speed, stamina drain)
- [ ] **Weight readout** — inventory HUD shows base capacity (e.g. 300), not 1000
- [ ] **Extra rows** — inventory shows +2 rows while active, slots styled like normal, inside the same window frame
- [ ] **Rows persist** — log out with items in extra rows, log back in: rows + items restored
- [ ] **Extra rows are real capacity** — fill the base rows, leave the extra rows empty, then walk over a dropped item: it is **auto-picked up into an extra row**, no "$msg_noroom". Repeat for manual pickup (E), a craft, and a container "take all"
- [ ] **Capacity survives a relog** — same test again *after* logging out and back in, and after dying and respawning (this is the drift the capacity patches guard against)
- [ ] **No false capacity** — with the blessing **inactive**, a full base grid still reports full (the patches must not raise the height without the blessing)
- [ ] **Drift log** — with `DebugLogging` on, `inventory height had drifted to N, restoring to M` appears if and only if something actually reset the height
- [ ] **Switch → crate** — switching to another blessing drops a CargoCrate at your feet with the extra-row items
- [ ] **Switch → multiple crates** — with all 16 extra slots full, switching spawns enough crates for everything (nothing left on the ground)
- [ ] **No speed / fall buff** — movement speed and fall-damage immunity are gone
- [ ] **No green VFX** — using/claiming a Forsaken Power, placing a Lord trophy, and drawing a blessing show NO green effects
- [ ] **Trophy VFX** — mounting a Lord trophy plays the non-green golden ceremony at the altar

#### With ComfyMods-ComfyQuickSlots installed

- [ ] **No crash on open** — opening the inventory does not throw (was `ArgumentOutOfRangeException` in `InventoryGridPatch.UpdatePlayerGrid` before the CQS-aware `BaseHeight` fix — see [systems.md](systems.md#comfyquickslots-compatibility))
- [ ] **Armor survives blessing switch** — equipped armor (CQS's row `y == 4`) is never crated when switching away from Featherweight
- [ ] **Extra rows survive logout under CQS** — log out with Featherweight items in the extra rows, log back in: rows + items restored (the renamed `"ComfyQuickSlotsInventory"` must still trigger the load pre-grow)
- [ ] **Backdrop covers all rows** — with Featherweight active, the dark backdrop extends to cover both the CQS armor/quickslot row AND the Featherweight rows beneath it, no gap or short frame
- [ ] **Chest window unaffected by CQS** — the storage window sits at the same configured offset with CQS installed as without it, and the CQS-positioned container grid keeps its relative offset inside the window
- [ ] **Extra rows actually RECEIVE items under CQS** — the single most important CQS test, and the one 0.6.6 passed while still being broken. Fill rows 0-4 (including the CQS armor/quickslot row), leave the Featherweight rows empty, then: walk over a dropped item (auto-pickup), press E on one (manual pickup), and finish a craft. Each must land **in a Featherweight row**. CQS's slot-finder hardcodes `y < 5`, so before 0.6.7 all three said "inventory full" while the rows sat visibly empty — see [systems.md](systems.md#capacity-is-not-reachability)
- [ ] **Unequip with a full base grid** — with rows 0-4 full and a Featherweight row free, unequip a piece of armour: it moves into a Featherweight row rather than vanishing (CQS's `Humanoid.UnequipItem` asks `GetEmptyInventorySlot` for a slot and does not check the result)
- [ ] **Height correction still runs under CQS** — with `DebugLogging` on, the drift log still appears when expected. CQS prefixes four of the five capacity methods and returns `false`; without `[HarmonyPriority(Priority.First)]` our prefixes are skipped entirely (see [systems.md](systems.md#harmony-prefix-ordering-vs-mods-that-replace-a-method))

### Storage window placement

- [ ] **Default position** — with default config, opening a chest puts the storage window 2 rows below the player inventory; with Featherweight active the extra rows are fully visible, not hidden behind it
- [ ] **Drag** — click and hold anywhere on the storage window that isn't an item slot (backdrop, header, weight readout) and drag: the whole window (backdrop + header + grid) follows the cursor as one piece
- [ ] **Slots still work** — clicking and dragging on an item slot moves the item as normal, not the window
- [ ] **Drag persists** — drop the window somewhere, close and reopen the chest: it's still there; check `UI.StorageUiOffsetColumns` / `StorageUiOffsetRows` in the config file were rewritten
- [ ] **Survives relog** — the dragged position is restored after logging out and back in
- [ ] **Can't be lost off-screen** — dragging hard toward any screen edge stops with part of the window still visible
- [ ] **Config applies live** — editing `StorageUiOffsetRows` in a config manager moves the window immediately, without reopening the chest
- [ ] **Reset** — setting both `UI` values back to `0` puts the window exactly at its vanilla spot (flush under the player inventory)
- [ ] **Not server-synced** — on a dedicated server the `UI` values stay client-side (each player keeps their own window position; the server does not overwrite them)

### Greydwarf Shaman Lord specific

- [ ] **Poison Orb** — ranged hits deal 36 pure poison (BepInEx log `greydwarf_lord hit` shows `poison=36 blunt=0`); no blunt conversion
- [ ] **Root Spawn** — BepInEx log shows `root prefab resolved: 'TentaRoot'`; root creature appears at player position 0.6 s after `fx_gdking_rootspawn` VFX; only 1 active root allowed outside Frenzy; root retracts/despawns after ≤30 s
- [ ] **Frenzy Root Spawn** — 3 roots appear in a ring ~5 m around the player (evenly spaced ≈120° apart); not clustered together
- [ ] **Healing Resonance** — shaman cast animation plays; green heal-burst VFX appears; Lord HP increases (if not full); nearby Greydwarves HP increases (if not full); **no damage to the player**; does not fire when Lord is already at full HP; ≥15 s between casts; disabled in Frenzy
- [ ] **Poison Nova** — fires at ≤50% HP; melee hits after nova show `poison=18` in log; profile mutation persists for rest of fight
- [ ] `Captured shaman heal cast: anim='<name>', N effect(s).` appears in log on startup (confirms heal VFX capture succeeded)
- [ ] **Forest's Embrace blessing** — with `SE_GreydwarfLordSpirit` active: planted crops within 30 m grow ~30% faster (Quick Sprout half); sitting beside a mature tree with no monsters within 30 m heals every 3 s, scaling with tree tier, and after 30 s seated grants an extended Rested buff (tree-rest half, folded in from the old Forsaken Power in 0.6.3)
- [ ] **Rootward FP** — pressing F with 1–5+ hostiles nearby heals the caster up to 100 HP and erupts a TentaRoot on each of the closest hostiles (capped at 5; fewer enemies ⇒ fewer roots); roots fight the caster's enemies and never damage the caster; log shows `Rootward root prefab resolved: 'TentaRoot'` and `Rootward: healed N HP, erupted N protective root(s)`; roots despawn within 30 s
- [ ] **Rootward migration** — a save with the old `GP_ForestsEmbrace` (or `GP_VerdantWrath`) equipped auto-upgrades to `GP_Rootward` on spawn, with a center message and `Migrated <old> → GP_Rootward` in the log

### Fenring Lord specific

- [ ] **Bat Summon** — `Taunt` animation plays each trigger; never more than 4 Bats within 16 m
- [ ] **Vampiric Strike** — activates at ≤80% HP; `fx_GP_Activation` VFX + blood-red aura on trigger; each landed melee hit heals ~80% of the damage it dealt (check via `Heal` amount vs. hit damage in log/observed HP); buff does **not** activate while Blood Frenzy is active
- [ ] **Shadow Fade** — only triggers at ≤60% HP; `Taunt` plays and Bats spawn immediately, but the Lord stays visible for ~1 s before renderers hide; invisible for 12 s; footstep/ground-dust particles remain visible while invisible; 60 s cooldown between activations
- [ ] **Blood Frenzy** — aura turns crimson at ≤30% HP; jump attacks fire noticeably more often than claw swings once frenzied (verify the jump-attack item logged/observed as prioritized)

### Lox Lord specific

- [ ] **Bone Bulwark** — pops only when a player is within 5 m; lasts 2.5 s; `fx_guardstone_activate` VFX on activation; incoming hits during the window are reduced ~65% (compare logged/observed damage to an unshielded hit); ~18 s before it can pop again (faster once Raging)
- [ ] **Unyielding Bulwark** — fires once at ≤40% HP; Lord is rooted (speed 0) for 5 s; incoming hits reduced ~80% during the window; Lord's HP visibly climbs back ~20% of max by the end; root holds even if Rage triggers mid-window; does not fire a second time later in the fight
- [ ] **Roaring Bellow** — fires once at ≤50% HP; 15 m knockback wave; light blunt damage
- [ ] **Rage** — speed ×1.5 and aura turns crimson at ≤30% HP; persists for the rest of the fight
- [ ] **Hearth Master** — food buffs last +100% longer with `SE_LoxLordSpirit` active (compare buff timer to baseline)
- [ ] **Bull Rush FP** — cannot be staggered; attacks cost −50% stamina; adrenaline fills noticeably faster than vanilla

### Howl of the Pack specific

- [ ] Nearby tamed creatures heal to full on activation
- [ ] Empowered tames show violet `HowlAura` glow
- [ ] Phantom Wolf spawns with violet tint, name = "Phantom Wolf"
- [ ] Phantom Wolf cannot be petted or renamed (no interaction prompt)
- [ ] Phantom Wolf follows player
- [ ] Phantom Wolf despawns after 60 s (base) or 120 s (synergy)
- [ ] Synergy (Pack Whisperer active): tamed wolf damage taken = −85%

### Pack Whisperer specific

- [ ] Without Howl: tamed wolves take −50% damage
- [ ] With Howl active simultaneously: tamed wolves take −85% damage
- [ ] Howl-only: tamed wolves deal +100% damage
- [ ] Taming speed: `biomelords_tame_time` shows remaining tame time falling twice as fast
      while the blessing is active within 30 m, vs. without it
- [ ] Breeding rate: tamed creatures breed roughly 2× as often with the blessing active

---

## PDF handbook

The handbook at `Docs/BiomeLords_Handbook.pdf` is generated from
`Docs/generate_lord_handbook.py` using ReportLab.

```powershell
cd "E:\Valheim Modding\ValheimBiomeLords\Github\BiomeLords\Docs"
python generate_lord_handbook.py
```

**Update the handbook whenever:**
- A Lord's ability, FP, or blessing changes
- A numeric constant changes (damage, duration, radius, chance)
- A new Lord or system is added

---

## Common error patterns and fixes

| Error | Cause | Fix |
|---|---|---|
| `Procreation_UpdateProcreation_PackBreed` crash | `UpdateProcreation(float dt)` doesn't exist | Use `[HarmonyTargetMethod]` targeting `Procreate` (parameterless) |
| `Smelter_Spawn_RefinersTouch` IL error | Parameter named `ore` not `name`; no `spawnQueueCount` | Fix signature to `(Smelter __instance, string ore, int stack)` |
| Refiner's Touch drops nothing / wrong item | `ore` is the **input** name (`m_from`), not the output; spawning `GetPrefab(ore)` drops raw ore and skips `ItemDrop.OnCreateNew` | Resolve `m_to` from `m_conversion`, instantiate at `m_outputPoint`, set stack, call `ItemDrop.OnCreateNew` |
| `SE_Rested_CalculateComfort` ambiguous match | Multiple overloads in newer Valheim | Use `[HarmonyTargetMethod]` to select `(Player) → int` overload |
| `Minimap.AddPin` compile error | `Splatform.dll` not referenced; newer overload uses `PlatformUserID` | Call `AddPin` via reflection, select smallest-arity overload |
| `PatchAll` aborts mid-way | One bad target kills everything | Use per-class patching loop with try/catch (already in Plugin.Awake) |
| `Patch class … failed: Patching exception in method null` | Explicit `new[] { typeof(...) }` array no longer matches any overload | See *Surviving a Valheim update* — extend the array |
| `Patch class … failed: Ambiguous match` | Vanilla gained an overload; patch has no arg list | `TargetMethods()` over all overloads, or add the arg list |
| `Patch class … failed: IL Compile Error` | `Prefix`/`Postfix` parameter named after a renamed vanilla parameter | Rename to match (`verify_patch_targets.py` catches this offline) |
| `MissingMethodException` at runtime through mod code | Called vanilla method gained a parameter; DLL built against old assembly | Rebuild — no code change |
| Build: `Could not locate the assembly "BepInEx"` | `GaleProfilePath` points at a profile without BepInEx/Jotunn | Fix the path in `BiomeLords.csproj` |
| Build: `MSB3021 … user-mapped section open` on the server copy | Dedicated server is running and has the DLL loaded | Stop the server, then copy; the compile itself succeeded |
| Chest/storage window only drags in the top of the screen | Clamp measured against the parent rect and re-ran every drag frame | Fixed 0.6.12 — clamp in screen pixels, on drag **end** only (`StorageWindowPosition.ClampToScreen`) |
| SE not appearing on HUD | Icon not assigned before first render | `StatusEffectFactory.EnsureIcon` lazy-assigns from trophy icon or falls back to any vanilla SE icon |
