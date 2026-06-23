# BiomeLords — Lord Reference

All seven Lords follow the same pattern: cloned vanilla prefab → scaled up → custom brain MonoBehaviour → boss bar. Damage is handled by `LordDamageBoostPatch`, which replaces every melee hit's damage values with the Lord's `DamageProfile × mult` (admin config, default 1.0×). Abilities live in per-Lord brain files under `Phase1B/` or `Phase1D/`.

---

## Neck Lord

| Field | Value |
|---|---|
| **Prefab** | `NeckLord` (clone of `Neck`) |
| **Biome** | Meadows (tier 1) |
| **Scale** | 1.6× |
| **Tint** | Crimson |
| **Base HP** | 500 |
| **Damage profile** | 6 slash (vanilla Neck bite ×1.2) |
| **Brain file** | `Phase1B/NeckLordBrain.cs` |

### Abilities

**Tide Caller** — every 45 s spawns up to 2 regular Necks (cap 3 nearby within 20 m). 15 s grace period on spawn.

**Water Blob** — at 5–18 m range and within a 60° forward arc, lobs a water projectile every 12 s. Pauses 1 s to telegraph the throw. In Frenzy, fires a triple-spread (left/centre/right).

**Block Phase** (≤50% HP) — when the player is within 6 m and actively attacking, the Lord raises a block for 2.5 s (12 s inter-cooldown). Indicated by a `fx_guardstone_activate` VFX pulse.

**Frenzy** (≤30% HP) — speed ×1.5, tint shifts to bright saturated red, continuous red-lightning spark aura every 0.4 s. Water Blob fires a triple-spread instead of a single shot.

### Drops
- Trophy of the Neck Lord (1, one-per-player)
- Copper (3–5)

### Blessing — Fisher's Boon
Marker SE (`SE_NeckLordSpirit`). Active effects (Harmony patches):
- Fishing bait is not consumed on a missed cast.
- Each fish pickup has a chance to yield a bonus fish.

### Forsaken Power — Tide's Grace (`GP_TidesGrace`)
Marker SE granting mastery of water. Duration 600 s, cooldown 1200 s. While active:
- **Swim → restore stamina** — `PowerEffectsService.TickTidesGrace` adds ~25 stamina/s while the player is swimming (net positive vs. the vanilla swim drain).
- **Wet → +50% melee** — `Character_Damage_TidesGraceMelee` (`Patches/PowerCombatPatch.cs`) scales blunt/slash/pierce ×1.5 when the local player attacks with a melee skill (Swords/Knives/Clubs/Polearms/Spears/Axes/Unarmed) while carrying the `Wet` status.
- **Wet debuff nullified** — `NeckWetImmunityPatch` skips the `Wet` effect's `ModifyDamageMods` and `ModifyStaminaRegen` on the local player, so the player still counts as Wet (the melee bonus applies) but takes none of its penalties.

---

## Greydwarf Shaman Lord

| Field | Value |
|---|---|
| **Prefab** | `GreydwarfShamanLord` (clone of `Greydwarf_Shaman`) |
| **Biome** | Black Forest (tier 2) |
| **Scale** | 1.7× |
| **Aura** | Bright-green child Light |
| **Base HP** | 2 500 |
| **Damage profile** | 17 slash (vanilla Greydwarf Shaman scratch ×1.2) |
| **Brain file** | `Phase1D/GreydwarfLordBrain.cs` |

### Abilities

**Sapling Summon** — every 60 s spawns up to 2 Greydwarves (cap 3 within 12 m). 15 s grace on spawn.

**Root Spawn** — every 40 s, if the player is 4–25 m away, telegraphs with `fx_gdking_rootspawn` then erupts a vanilla `TentaRoot` under the player's feet (30 s safety despawn). In Frenzy, spawns 3 roots in a ring (5 m radius) around the player instead of one.

**Healing Resonance** — every 15 s (disabled in Frenzy), plays the genuine shaman heal-cast animation, then heals itself and all Greydwarves within 12 m for 60 HP each. Skips targets already at full HP. The vanilla heal item is stripped at build time to prevent its AoE from firing through `LordDamageBoostPatch`; the animation and VFX are captured and replayed in code.

**Poison Nova** (≤50% HP, once) — erupts a poison burst VFX and permanently adds 18 poison to every subsequent melee hit for the rest of the fight (mutates the per-instance `LordProfileRegistry` entry so the damage patch picks it up).

**Frenzy** (≤30% HP) — speed ×1.5, lime-green tint flash, Healing Resonance disabled, continuous spark aura. Root Spawn switches to the surrounding ring-of-3 pattern.

### Drops
- Trophy of the Greydwarf Shaman Lord (1, one-per-player)
- Bronze (3–5)
- Surtling Core (1–2)
- Coal (5–10)

### Blessing — Quick Sprout
Marker SE (`SE_GreydwarfLordSpirit`). Active effect (`Patches/QuickSproutGrowthPatch.cs`):
- Planted crops within 30 m grow about 30% faster while the blessing is active.

### Forsaken Power — Forest's Embrace (`GP_ForestsEmbrace`)
Marker SE; `ForestEmbraceService` ticks while this SE is on the player:
- Standing by any mature tree counts as shelter.
- Sitting beside a tree with no monsters within 30 m heals the player every 3 s — the older
  the tree, the stronger the gift (1 HP near Beech/Birch up to 5–6 HP near Yggdrasil/Charred
  trees).
- After 60 s of seated rest near a tree, `ForestEmbraceComfortPatch` elevates comfort so
  vanilla grants a longer Rested buff (Beech/Birch +1, Oak +2, Yggdrasil/Charred +3).

---

## Draugr Elite Lord

| Field | Value |
|---|---|
| **Prefab** | `DraugrEliteLord` (clone of `Draugr_Elite`) |
| **Biome** | Swamp (tier 3) |
| **Scale** | 1.3× |
| **Aura** | Sickly-green child Light |
| **Base HP** | 5 000 |
| **Damage profile** | 58 slash (vanilla Draugr Elite sword swing) |
| **Brain file** | `Phase1D/DraugrLordBrain.cs` |

### Abilities

**Rotten Cleave** (cooldown 30 s, range 5 m)
- Lord freezes and plays the vanilla `fx_GP_Activation` forsaken-power column as a 1 s telegraph.
- Performs a real equipped-sword swing via `Humanoid.StartAttack`; the animation hitbox carries the damage — dodge rolls avoid it entirely.
- Paired slice VFX (`vfx_cut` / `vfx_swing_sledge` / `vfx_HitSparks`) spawns at the sword tip 0.4 s into the swing.
- **Wounded** debuff: applied only when the swing's damage actually reduces the player's HP. Fully-blocked and parried hits grant no bleed (checked via pre/post HP delta in `DraugrWoundPatch`).
  - Bleeds 5 HP/s for **60 seconds** (`SE_DraugrWound`). Blood-splatter VFX (`vfx_BloodHit`) fires on the player each tick.
  - Re-landing a Rotten Cleave refreshes the duration.

**Plague Cloud** (≤60% HP, once)
- Spawns a stationary `PlagueCloud` at the Lord's current position (stays fixed even if the Lord moves).
- Ticks 5 poison/s to any player within 5 m for 25 s.
- Activation: blob-attack VFX burst ring + center message.

**Undying Surge** (≤40% HP, once)
- Lord freezes; plays a healing VFX ring (`vfx_HealthUpgrade` + `vfx_Potion_health_medium` ring of 6).
- After a 1.5 s pause, instantly kills **every non-player creature within 60 m**. Kill hits are attacker-null so `LordDamageBoostPatch` is bypassed and resistances cannot negate them.
- Heals **500 HP per creature** killed.
- Fails (0 heal, failure message) if no creatures are in range.
- Blocks `TryRottenCleave` and `TrySummonMinions` while active.

**Summon Draugr** (every 60 s, max 3 within 12 m)
- Spawns 2 vanilla `Draugr` per cycle, cap-clamped so nearby count never exceeds 3.
- Suppressed while Undying Surge is active.

**Death Throes** (≤25% HP, permanent)
- Speed ×1.5 for the remainder of the fight.
- Aura Light shifts to blood-red at higher intensity and range.
- One-shot activation burst (blob-attack VFX + corpse explosion + red lightning ring of 8).
- Continuous red-lightning pulse every 0.6 s while active.
- Applies the vanilla **Wet** status to any player within 3 m every 2 s.

### Drops
- Trophy of the Draugr Elite Lord (1, one-per-player)
- Iron (2–4)
- Coal (5–10)
- Bone Fragments (5–10)
- Entrails (3–5)

### Blessing — Iron Vein
Marker SE (`SE_DraugrLordSpirit`). Active effects (Harmony patch):
- Mining iron from Swamp ore has a chance to yield an extra piece.

### Forsaken Power — Plague Bearer (`GP_PlagueBearer`)
- Full **poison immunity**.
- +75% outgoing poison damage.
- +50% health regeneration rate.

---

## Fenring Lord

| Field | Value |
|---|---|
| **Prefab** | `FenringLord` (clone of `Fenring`) |
| **Biome** | Mountain (tier 4) |
| **Scale** | 1.4× |
| **Aura** | Pale-blue child Light (shifts crimson in Blood Frenzy, blood-red during Vampiric Strike) |
| **Base HP** | 7 500 (matched to Moder's tier) |
| **Damage profile** | 85 slash (vanilla Fenring claw scratch) |
| **Brain file** | `Phase1D/FenringLordBrain.cs` |

### Abilities

**Bat Summon** — every 60 s spawns up to 2 Bats (cap 4 within 16 m). Plays the `Taunt` animation as a tell each time it triggers.

**Vampiric Strike** (≤80% HP) — activates a 60 s buff (40 s cooldown after it expires): every landed melee hit absorbs 80% of the post-armor damage dealt back as healing (`Character_Damage_FenringVamp` in `Patches/FenringVampPatch.cs`). Triggers the vanilla Forsaken Power activation VFX (`fx_GP_Activation`) and a blood-red aura glow. **Disabled once Blood Frenzy is active** — the two abilities don't stack.

**Shadow Fade** (≤60% HP) — every 60 s: plays the `Taunt` animation and summons a fresh wave of Bats (same cap as Bat Summon), then after a 1 s delay turns invisible for 12 s. All mesh renderers are hidden except `ParticleSystemRenderer`, so footstep/ground-dust particles stay visible as a tracking hint.

**Blood Frenzy** (≤30% HP, permanent) — speed ×1.5, crimson aura, `MonsterAI.m_jumpInterval` cut to 20% of base, `m_minAttackInterval` cut to 30% of base. Additionally biases the AI's attack-item selection directly: the jump-attack weapon item (prefab name contains `"jump"`, e.g. `Fenring_attack_jump`) gets its `m_aiAttackInterval` shrunk to 15% and `m_aiPrioritized = true`, while every other attack item's interval is multiplied ×5 — so jump attacks fire far more often than claw swings.

### Drops
- Trophy of the Fenring Lord (1, one-per-player)
- Wolf Fang (3–5)
- Wolf Pelt (2–4)
- Freeze Gland (1–2)
- Silver (1–2)

### Blessing — Pack Whisperer
Marker SE (`SE_FenringLordSpirit`). Purely defensive/breeding — it does **not** grant any
+damage-dealt buff (that's exclusive to the Howl of the Pack Forsaken Power below). Active
effects (Harmony patches):
- Tamed wolves within 30 m take −50% damage while the bearer is nearby (`TamedWolfPatch`).
- Taming speed for any untamed creature is doubled while the bearer is within 30 m and the blessing is active (`Tameable_DecreaseRemainingTime_PackWhisperer` in `Patches/PackBreedingPatch.cs`).
- Tamed-creature breeding rate is doubled (`Procreation_Procreate_PackBreed` in the same file).

### Forsaken Power — Howl of the Pack (`GP_HowlOfThePack`)
Marker SE; `PowerEffectsService` amplifies nearby tamed-wolf damage via the same `TamedWolfPatch` check and spawns auto-despawning Phantom Wolf companions on activation. No innate stat mods on the player — the gift is the pack. Duration 600 s, cooldown 1200 s.

**Synergy with Pack Whisperer** (the blessing active when the power is pressed):
- Phantom wolves **ignore all incoming damage** (`PhantomWolfInvulnPatch` swallows the hit when the defender carries a `PhantomWolf` marked `Invulnerable`).
- **Blood Magic scales the pack** — skill ≥ 50 summons 2 wolves, skill 100 summons 3 (otherwise a single wolf).

Phantom wolves keep their `Tameable` (so vanilla's tamed-creature systems don't warn/NRE) but are made non-commandable and have `Procreation` stripped. Their `ZNetView` ZDO is marked non-persistent, so they are never saved — they despawn on lifetime, when the local player is gone, or on logout.

---

## Lox Lord

| Field | Value |
|---|---|
| **Prefab** | `LoxLord` (clone of `Lox`) |
| **Biome** | Plains (tier 5) |
| **Scale** | 1.25× |
| **Aura** | Dusty-yellow child Light (shifts crimson during Rage) |
| **Base HP** | 10 000 (matched to Yagluth's tier) |
| **Damage profile** | 130 slash (vanilla Lox bite, unmodified) |
| **Brain file** | `Phase1D/LoxLordBrain.cs` |

### Abilities

**Untamable** — the vanilla Lox's `Tameable` (and `Procreation`) components are stripped
from the prefab clone in `CreatureFactory.BuildLoxLord`, so the Lox Lord can never be fed,
tamed, or bred. (Removing `Tameable` without `Procreation` would NRE in `Procreation.Procreate`.)

No minion summons and no extra attack abilities — vanilla Lox already hits hard enough,
so the Lox Lord's brain is entirely defensive aside from one rage panic button:

**Bone Bulwark** — reactive ward: every ~18 s (12 s in Rage), if a player is within melee
range (5 m), raises a ward for 2.5 s that mitigates 65% of all incoming damage (blockable
or not). Indicated by a `fx_guardstone_activate` VFX pulse. Mitigation is implemented in
`Patches/LoxLordShieldPatch.cs`, which reads `LoxLordBrain.IsShielded`.

**Unyielding Bulwark** (≤40% HP, once) — roots in place for 5 s, mitigating 80% of all
incoming damage while healing back 20% of max HP spread linearly over the window. The
root is re-asserted every frame so a mid-window Rage trigger can't undo it. Mitigation
shares `LoxLordShieldPatch.cs` with Bone Bulwark, gated on `IsLastStand` instead.

**Roaring Bellow** (≤50% HP, once) — 15 m stagger/push wave; light blunt damage,
knocks every player in range back.

**Rage** (≤30% HP, permanent) — speed ×1.5, aura shifts to crimson, continuous
red-lightning pulse.

### Drops
- Trophy of the Lox Lord (1, one-per-player)
- Lox Meat (3–5)
- Lox Pelt (2–4)
- Barley (8–15)
- Black Metal Scrap (1–3)

### Blessing — Hearth Master
Marker SE (`SE_LoxLordSpirit`). Active effect (`Patches/HearthMasterPatch.cs`):
- Food buffs you eat last +100% longer (`LordConfig.HearthMasterMultiplier`, default 2.0×).

### Forsaken Power — Bull Rush (`GP_BullRush`)
Stat SE — the "unstoppable" identity:
- Cannot be staggered.
- Attacks cost −50% stamina.
- Adrenaline surges +100% with every strike.

---

## Seeker Lord

| Field | Value |
|---|---|
| **Prefab** | `SeekerLord` (clone of `Seeker`) |
| **Biome** | Mistlands (tier 6) |
| **Scale** | 1.4× (reads as "queen-class") |
| **Aura** | Violet child Light (shifts crimson in Hive Frenzy) |
| **Base HP** | 12 500 (matched to the Queen's tier) |
| **Damage profile** | 120 pierce (vanilla Seeker claw thrust, unmodified) |
| **Brain file** | `Phase1D/SeekerLordBrain.cs` |

### Abilities

The fight escalates with the Lord's wounds: melee-only above 80% HP, then ranged
pressure (Acid Spit), then a ground gap-closer (Burrow Ambush), then enrage (Hive Frenzy).
Acid Spit and Burrow Ambush both commandeer the `MonsterAI` while they run and are mutually
exclusive via a shared `_inSpecial` guard, so the two can never overlap.

**Acid Spit (Aerial)** (≤80% HP, every 24 s) — when a player is within 22 m **and in line of
sight** (vision-gated via `MonsterAI.CanSeeTarget`, like Gjall's spit), the Lord takes off using
the engine's built-in flight system (`Character.TakeOff`/`Land`) with the AI suspended and
movement driven by `SetMoveDir`. It flies out to a ~16 m standoff (`StandoffSeekDir`) — staying
a distance in front of the player rather than hovering overhead — then rains down acid volleys.
Each volley:
- Fires **only while airborne and facing the player** (forward-cone dot check), with **scattered
  (inaccurate) aim** (up to ~4 m offset) so the player can read and sidestep it.
- Launches a glowing/dripping acid blob (code-built emissive sphere + acid trail + continuous
  particle spray + glow Light), arced to the snapshotted impact point.
- Lands a 3.5 m AoE: 12 poison + 6 pierce + 25 push (damage via `LordDamageBoostPatch`).
- Plays **genuine vanilla acid VFX *and* SFX**: `SeekerLordBrain.ResolveSpitAssets()` reads the
  launch + impact `EffectList`s off a live vanilla creature at runtime (priority **Gjall** →
  **SeekerQueen** non-teleport → **Seeker** melee as a sound-of-last-resort), since the shipped
  asset files don't expose FX/SFX prefab names as strings. No vanilla projectile prefab is ever
  instantiated (cloning the Queen's networked teleport projectile corrupted `ZNetScene`); only
  the safe client-side effect lists are replayed.

**Burrow Ambush** (≤60% HP, every 22 s) — **ground-only** (skips while flying/mid-jump), when a
player is 6–25 m away and in line of sight: a 0.6 s dig-in tell (dramatic dust/debris burst via
`SpawnDigBurst` — stomp dust + flying debris + mist puff + sparks + a 6-point debris ring), then
the Lord vanishes (mesh renderers hidden, particle renderers kept) and dashes underground toward
the player kicking up a debris trail. It resurfaces with the same burst, then after a 0.4 s
telegraph erupts beneath the player: a 4 m radius knock-up burst (50 push, biased strongly
upward).

**Brood Call** (every 60 s) — spawns up to 2 `SeekerBrood` (cap 3 nearby within 16 m, cap-clamped).

**Hive Frenzy** (≤30% HP, permanent) — speed ×1.5, aura shifts crimson, all ability cooldowns
×0.6, continuous spark/red-lightning aura pulse.

### Drops
- Trophy of the Seeker Lord (1, one-per-player)
- Carapace (3–5)
- Sap (2–4)
- Eitr (1–3)
- Mandible (1–2)

### Blessing — Refiner's Touch
Marker SE (`SE_SeekerLordSpirit`). Active effect (`Patches/RefinersTouchPatch.cs`):
- A Smelter / Blast Furnace / Spinning Wheel / Eitr Refinery within 30 m has a 50% chance
  (`RefinersTouchChance`, admin-configurable) to drop a bonus copy of the **refined output**
  when it completes a product — e.g. an extra Copper, LinenThread, or Eitr, not the raw input ore.

### Forsaken Power — Hive Sight (`GP_HiveSense`)
Marker SE; `HiveSightService` (driven by `PowerEffectsService`) runs while the power is active:
- Every hostile creature within 80 m is pinned on the minimap (refreshed each second, pruned on
  death / distance) and given a faint through-wall glow pulse — visible even through stone and mist.

---

## Fallen Valkyrie Lord

| Field | Value |
|---|---|
| **Prefab** | `FallerValkyrieLord` (clone of `FallenValkyrie`, the Ashlands boss) |
| **Biome** | Ashlands (tier 7) |
| **Scale** | 1.3× |
| **Aura** | Radiant white-gold child Light (brightens in Rage) |
| **Base HP** | 25 000 (matched to the Fallen Valkyrie boss's tier) |
| **Damage profile** | 160 pierce (vanilla Fallen Valkyrie claw, unmodified) |
| **Brain file** | `Phase1D/FallerValkyrieLordBrain.cs` |

Cloned from the vanilla `FallenValkyrie` boss so the Lord keeps **genuine flight** via the
engine's `Character.TakeOff`/`Land` system — Dive Bomb and Soul Harvest both fly. ("Fader"
was the boss's old internal codename; the live prefabs are `FallenValkyrie` /
`TrophyFallenValkyrie`.)

### Abilities

The fight escalates with the Lord's wounds: Wind Gust at any HP, then a diving gap-closer
below 80%, then a self-heal + enrage below 30%. **Every ability opens with a cast tell** —
an alert/taunt VFX flash on the Valkyrie's body (`SpawnAbilityTell`, `fx_fallenvalkyrie_alert`
+ `fx_fallenvalkyrie_taunt` + himminafl flash) followed by a 0.5 s wind-up — so the player
always gets a visual warning before it fires. Dive Bomb and Soul Harvest both commandeer the
`MonsterAI` while airborne and are mutually exclusive via a shared `_inSpecial` guard.

**Wind Gust Knockback** (any HP, every 16 s) — when a player is within 7 m: a short spin-up
using the Valkyrie's own wing-spin charge FX (`fx_fallenvalkyrie_attack_spin_charge` +
`fx_valkyrie_flapwing`), then a radial gust (release FX `fx_fallenvalkyrie_attack_spin_release`)
that pushes every player within 6 m back (50 push). A positioning tool that fits Ashlands' cliff
terrain — little damage, all knockback.

**Dive Bomb Slam** (≤80% HP, every 13 s) — when a player is within 20 m: takes off, climbs
~10 m above the player's snapshotted position, telegraphs the strike point, then dives straight
down onto it. The **slam VFX fires on ground impact** (`fx_fallenvalkyrie_attack_claw` +
himminafl + meteor explosion), landing a 4 m AoE with 70 push.

**Soul Harvest** (≤30% HP, every 30 s — locks out again above 50% HP) — armed only while wounded
(hysteresis: arms below 30%, disarms once self-healing pushes it back above 50%). When a player is
within 12 m, the Valkyrie **takes off and holds station airborne**, periodically wingflapping, and
for 6 s tethers to every player within 10 m — a chain of light FX strung along the line between her
and each player (`fx_fallenvalkyrie_attack_spit_projectile` anchor → spark beam →
`..._impact`) — draining 6 spirit/tick (every 0.5 s) and healing herself for the total drained.

**Rage** (≤30% HP, permanent) — speed ×1.5, aura brightens to radiant white-gold, a screech
burst on activation, all ability cooldowns ×0.6, continuous spark pulse every 0.4 s.

### Drops
- Trophy of the Fallen Valkyrie Lord (1, one-per-player)
- Flametal (2–4)
- Coal (10–20)
- Charred Bone (3–6)
- Sulfur Stone (4–8)

### Blessing — Featherweight
A pure **hauling** blessing (`SE_FallerValkyrieLordSpirit`) — a marker SE with **no**
`SE_Stats` modifiers. The Valkyrie's wings bear your burdens in two ways, both driven by
runtime logic gated on the SE being active:

- **Raised carry-weight cap with no penalty** (`LordConfig.FallerValkyrieWeightCap`, default
  **1000**). Vanilla keys every over-encumbrance penalty off `IsEncumbered()` →
  `GetTotalWeight() > GetMaxCarryWeight()`. `FeatherweightEncumbrancePatch` postfixes
  `Player.GetMaxCarryWeight` to report the cap, so **below 1000 you suffer nothing** — you
  walk, run, dodge, regenerate stamina and auto-pickup normally with no encumbered
  animation. Only **at** the cap does the normal encumbered state engage. The inventory
  weight readout still shows your **base** capacity (e.g. 300/450 w/ Megingjord), not the
  cap — a second postfix on `InventoryGui.UpdateInventoryWeight` rewrites the HUD number;
  the cap is communicated through this tooltip / compendium instead.
- **+2 inventory rows** (`LordConfig.FallerValkyrieExtraRows`, default 2 — 8 slots each),
  via `FeatherweightInventory`. The extra rows use the **same** slot UI as the normal grid
  (built from the same `m_elementPrefab`); `InventoryGui_Show_FeatherweightPanel` stretches
  the inventory window/backdrop to wrap them. Because Valheim doesn't persist inventory
  dimensions, the rows are restored each session: `InventoryExpandLoadPatch` pre-grows the
  inventory before items load (so extra-row items aren't compacted/destroyed), and the spawn
  re-apply reconciles the height. **Switching to a different blessing** collapses the rows and
  spills their contents into one or more **`CargoCrate`s** dropped at your feet (the same
  crate a broken cart/ship leaves behind — `Container.DropAllItems`); a fresh crate is spawned
  whenever one fills, so a full set of extra slots never overflows onto the ground. On
  **death** the tombstone resizes to the expanded inventory (`MoveInventoryToGrave` copies
  width/height), so nothing is lost.

**Ceremony VFX** — mounting a Lord trophy plays a non-green golden ceremony (rune swirl +
golden pops + sparks ring) at the altar; drawing the blessing plays no extra VFX. None of the
Lord ceremonies or Forsaken Powers use green effects (the green `fx_summon_start` summon burst
is excluded everywhere).

> Featherweight no longer grants movement speed or fall-damage immunity — those were
> removed in this rework.

### Forsaken Power — Valkyrie's Rally (`GP_ValkyrieAscension`)
A one-shot **group support burst** — a marker SE driven by `ValkyrieRallyService`. On
activation the caster broadcasts a routed RPC (`BiomeLords_ValkyrieRally`, registered per
world-join by the `Game.Start` postfix in `ValkyrieRallyRpcPatch`). Every client whose local
player is within `LordConfig.ValkyrieRallyRadius` (default 20 m) of the caster restores
**itself** — so the heal lands correctly in multiplayer:
- **HP, Stamina and Eitr** filled to maximum.
- **Adrenaline** topped to max — only if a trinket is equipped (`Humanoid.m_trinketItem`,
  read via reflection).
- **Max-level StaffShield bubble** — a fresh `SE_Shield` cloned from the `StaffShield` item,
  applied at the staff's max quality and skill 100; any existing shield is removed first so
  the absorb pool is refreshed to full.
- **20-minute Rested buff** (`LordConfig.ValkyrieRallyRestedSeconds`, default 1200) — the
  vanilla `Rested` SE with its `m_ttl` overridden.

The SE itself carries no stat mods and lapses after a brief `InstantWindow` (4 s); the
20-minute cooldown is the real gate.
