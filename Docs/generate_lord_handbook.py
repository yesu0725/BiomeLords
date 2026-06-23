"""
BiomeLords — Lord Handbook generator.

Builds Docs/BiomeLords_Handbook.pdf from the LORDS data structure below.
Run after any gameplay change so the handbook stays current:

    python generate_lord_handbook.py

The script is intentionally self-contained — all Lord data lives here,
not parsed from C# source. When you tune a Lord, update its dict entry
in LORDS and re-run.

Damage columns in each lord's "attacks" list:
  (name, blunt, slash, pierce, fire, frost, lightning, poison, push, cd, notes)
  0 / None  → "—"  (damage type not used)
  integer   → base coded value (effective = base × eff_mult shown separately)
  "V"       → vanilla-inherited value scaled by eff_mult
  "SE"      → status-effect only, no direct damage

Push force effective = coded value × 1.5 (LordDamageBoostPatch, always).
Damage effective = base × TierMult × IntrinsicMult.
Plague Cloud bypasses multiplier — value shown is fixed.
"""

from datetime import date
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.lib.pagesizes import LETTER
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


MOD_VERSION = "0.6.0"
GENERATED = date.today().isoformat()


# ---------------------------------------------------------------------------
# Data
# ---------------------------------------------------------------------------

LORDS = [
    {
        "id": "neck_lord",
        "name": "Neck Lord",
        "biome": "Meadows",
        "tier": 1,
        "prefab": "NeckLord",
        "base_prefab": "Neck",
        "hp": 500,
        "scale": 1.6,
        "aura": "Crimson",
        "intrinsic_dmg": 1.0,
        "kill_targets": ["Neck"],
        "default_kill_req": 20,
        "resistances": {
            "weak":      [],
            "resistant": [],
            "immune":    [],
            "note":      "All damage types Normal.",
        },
        # (name, blunt, slash, pierce, fire, frost, lightning, poison, push, cd, notes)
        "attacks": [
            ("Jaws",              0, 6, 0, 0, 0, 0, 0, "V", "AI",    "Vanilla melee"),
            ("Tidal Shot",  "18†", 0, 0, 0, 0, 0, 0, 25,   "12 s",  "Ranged blob; 5–18 m; 60° arc; 1 s pause; 1.5 m splash; ×3 in Frenzy"),
            ("Tide Shield",       0, 0, 0, 0, 0, 0, 0,  0,  "≤50% HP", "Reactive block; player attacking ≤6 m; 2.5 s window; 12 s inter-CD"),
            ("Tide Caller",       0, 0, 0, 0, 0, 0, 0,  0,  "45 s",  "Summon only — 2 Necks, max 3"),
            ("Frenzy ≤30% HP",    0, 0, 0, 0, 0, 0, 0,  0,  "once",  "+50% speed, red tint, sparks; Tidal Shot fires ×3"),
        ],
        "abilities": [
            ("Tidal Shot", "12 s", "Facing player (60° arc); range 5–18 m",
             "Freezes in place 1 s, then fires blue water blob projectile (sparkle trail). "
             "Detonates on arrival: 1.5 m splash, 18 blunt, blockable + dodgeable. "
             "Fires 3 projectiles (center ± 20°) when frenzied. "
             "†Damage subject to Lord profile patch at runtime."),
            ("Tide Shield", "2.5 s window / 12 s inter-CD", "HP ≤ 50% + player attacking within 6 m",
             "Reactive block: activates only when Player.InAttack() is detected. "
             "Cancels all blockable hits for 2.5 s. "
             "Ward flash (fx_guardstone_activate) on activation; "
             "fx_GoblinShieldHit on each blocked hit."),
            ("Tide Caller", "45 s", "Cap-gated (max 3 Necks within 20 m)",
             "Spawns 2 Necks 3 m flanking with vfx_spawn"),
            ("Frenzy", "One-shot", "HP ≤ 30%",
             "+50% speed, brighter red tint, periodic spark pulse. "
             "Tidal Shot fires 3 projectiles (center + ± 20° spread) instead of 1."),
        ],
        "drops": ["TrophyNeckLord ×1 (one per player)", "Copper ×3–5"],
        "trophy": "TrophyNeckLord (crimson retint of TrophyNeck)",
        "blessing": {
            "name": "Fisher's Boon",
            "se": "SE_NeckLordSpirit",
            "tooltip": "Fishing casts have a 50% chance to spare bait (FisherBoonBaitSaveChance); "
                       "landed fish have a 25% chance to drop a second fish (FisherBoonBonusFishChance).",
        },
        "power": {
            "name": "Tide's Grace",
            "gp": "GP_TidesGrace",
            "tooltip": "The Neck Lord's mastery of water flows through you. While swimming, "
                       "stamina is restored instead of drained. While Wet, melee attacks deal "
                       "+50% damage — and the Wet status can no longer harm you.",
        },
        "notes": [
            "m_avoidFire = false — fire-avoidance behaviour removed from the vanilla Neck clone.",
            "†Tidal Shot damage: HitData.SetAttacker points to the Neck Lord, so hits are "
            "routed through LordDamageBoostPatch and overwritten with the Lord's resolved "
            "attack profile (20 pierce at native tier, converging with progression).",
        ],
    },
    {
        "id": "greydwarf_lord",
        "name": "Greydwarf Shaman Lord",
        "biome": "Black Forest",
        "tier": 2,
        "prefab": "GreydwarfShamanLord",
        "base_prefab": "Greydwarf_Shaman",
        "hp": 2500,
        "scale": 1.7,
        "aura": "Green",
        "intrinsic_dmg": 1.0,
        "kill_targets": ["Greydwarf", "Greydwarf_Elite", "Greydwarf_Shaman"],
        "default_kill_req": 30,
        "resistances": {
            "weak":      ["Fire"],
            "resistant": ["Blunt"],
            "immune":    [],
            "note":      "Slash, Pierce, Frost, Lightning, Poison Normal.",
        },
        "attacks": [
            ("Poison Orb (ranged)",  0,  0, 0, 0, 0, 0, 30, "V",  "AI",   "Ranged projectile"),
            ("Jaws",                 0, 14, 0, 0, 0, 0,  0, "V",  "AI",   "Vanilla melee"),
            ("Root Spawn",           6,  0, 0, 0, 0, 0,  0, 30,   "40 s", "Range 4–25 m; 0.6 s tele; ×3 ring in Frenzy"),
            ("Poison Nova ≤50% HP",  0,  0, 0, 0, 0, 0,  0,  0,   "once", "Visual/msg only; adds 25 pois to melee profile"),
            ("Healing Resonance",    0,  0, 0, 0, 0, 0,  0,  0,   "15 s", "Heals self + Greydwarves +60 HP; off in Frenzy"),
            ("Sapling Summon",       0,  0, 0, 0, 0, 0,  0,  0,   "60 s", "2 Greydwarves, max 3 nearby"),
            ("Frenzy ≤30% HP",       0,  0, 0, 0, 0, 0,  0,  0,   "once", "+50% speed, lime tint, heal disabled, root ring"),
        ],
        "abilities": [
            ("Sapling Summon", "60 s", "Cap-gated (max 3 Greydwarves within 12 m)",
             "Spawns 2 Greydwarves 3.5 m flanking (cap-clamped)"),
            ("Root Spawn", "40 s", "Player within 4–25 m",
             "0.6 s telegraph → erupts vanilla TentaRoot under player; 30 s safety despawn. "
             "In Frenzy: 3 roots in a ring (5 m radius, ±18° jitter) around the player."),
            ("Healing Resonance", "15 s", "Not frenzied; any Greydwarf within 12 m",
             "Genuine shaman heal-cast animation, then heals self + all Greydwarves within 12 m "
             "for 60 HP each (skips full-HP targets). VFX replayed in code — vanilla heal item "
             "is stripped at build time to prevent its AoE from firing through LordDamageBoostPatch."),
            ("Poison Nova", "One-shot", "HP ≤ 50%",
             "Burst VFX + center message, then permanently adds +25 poison to every subsequent "
             "melee hit by mutating the per-instance LordProfileRegistry entry."),
            ("Frenzy", "One-shot", "HP ≤ 30%",
             "+50% speed, lime-green tint flash, Healing Resonance disabled, continuous spark aura. "
             "Root Spawn switches to the ring-of-3 surrounding pattern."),
        ],
        "drops": [
            "TrophyGreydwarfShamanLord ×1 (one per player)",
            "Bronze ×3–5",
            "SurtlingCore ×1–2",
            "Coal ×5–10",
        ],
        "trophy": "TrophyGreydwarfShamanLord (bright-green retint of TrophyGreydwarfShaman)",
        "blessing": {
            "name": "Quick Sprout",
            "se": "SE_GreydwarfLordSpirit",
            "tooltip": "Planted crops within 30 m grow about 30% faster while this blessing "
                       "endures (QuickSproutGrowthPatch).",
        },
        "power": {
            "name": "Forest's Embrace",
            "gp": "GP_ForestsEmbrace",
            "tooltip": "The trees themselves shelter you. Standing by any mature tree counts "
                       "as shelter. Sit beside a tree with no monsters within 30 m to heal — "
                       "the older the tree, the stronger the gift (1 HP near Beech/Birch up to "
                       "5-6 HP near Yggdrasil/Charred trees, every 3 s). After 60 s of seated "
                       "rest the forest grants a Rested buff; older trees extend it further "
                       "(Beech/Birch +1, Oak +2, Yggdrasil/Charred +3).",
        },
    },
    {
        "id": "draugr_lord",
        "name": "Draugr Elite Lord",
        "biome": "Swamp",
        "tier": 3,
        "prefab": "DraugrEliteLord",
        "base_prefab": "Draugr_Elite",
        "hp": 5000,
        "scale": 1.3,
        "aura": "Sickly green → blood-red at Death Throes (≤25% HP)",
        "intrinsic_dmg": 1.0,
        "kill_targets": ["Draugr", "Draugr_Elite", "Draugr_Ranged"],
        "default_kill_req": 25,
        "resistances": {
            "weak":      [],
            "resistant": ["Frost", "Poison"],
            "immune":    [],
            "note":      "Undead. Blunt, Slash, Pierce, Fire, Lightning Normal.",
        },
        "attacks": [
            ("Draugr Sword",                  0,  80,  0,  0, 0, 0, 50,         "V",  "AI",   "Profile-overridden: 80 slash + 50 poison"),
            ("Rotten Cleave",                 0,  80,  0,  0, 0, 0, 50,         "V",  "30 s", "5 m; fx_GP_Activation tele 1 s; real sword swing; Wounded SE on landed hit"),
            ("Plague Cloud  ≤60% HP",         0,   0,  0,  0, 0, 0, "5/s†",     0,    "once", "†Fixed 5 pois/s; bypasses mult; 5 m r, 25 s"),
            ("Undying Surge  ≤40% HP",        0,   0,  0,  0, 0, 0, 0,          0,    "once", "60 m r; instant-kills all creatures; 500 HP/kill"),
            ("Summon Draugr",                 0,   0,  0,  0, 0, 0, 0,          0,    "60 s", "2 Draugr, max 3 within 12 m"),
            ("Wet Touch",                     0,   0,  0,  0, 0, 0, 0,          0,    "2 s",  "Applies Wet SE in Death Throes; ≤3 m"),
            ("Death Throes  ≤25% HP",         0,   0,  0,  0, 0, 0, 0,          0,    "once", "+50% speed, blood-red aura, Wet Touch on"),
        ],
        "abilities": [
            ("Rotten Cleave", "30 s", "Player within 5 m",
             "Lord freezes; fx_GP_Activation forsaken-power column plays for 1 s (telegraph). "
             "Then Humanoid.StartAttack fires a real sword swing — damage rides the animation "
             "hitbox (80 slash + 50 poison x mult via LordDamageBoostPatch). Slice VFX at sword tip. "
             "Applies Wounded (SE_DraugrWound) only if the swing actually reduces player HP — "
             "a dodge or full block grants no bleed. "
             "Wounded: 5 HP/s for 60 s; vfx_BloodHit each tick; re-landing refreshes duration."),
            ("Plague Cloud", "One-shot", "HP ≤ 60%",
             "Spawns stationary PlagueCloud at current position (stays fixed if Lord moves). "
             "5 m radius, 25 s, 5 poison/sec. †Bypasses Lord dmg mult — value is fixed."),
            ("Undying Surge", "One-shot", "HP ≤ 40%",
             "Lord freezes; healing VFX ring (vfx_HealthUpgrade + vfx_Potion_health_medium x6). "
             "After 1.5 s, instantly kills every non-player creature within 60 m. "
             "Kill hits are attacker-null so LordDamageBoostPatch is bypassed — resistances "
             "cannot negate them. Heals 500 HP per creature killed. "
             "Fails (0 heal + failure message) if no creatures are in range. "
             "Blocks Rotten Cleave and Summon Draugr while active."),
            ("Summon Draugr", "60 s", "Cap-gated (max 3 Draugr within 12 m)",
             "Spawns up to 2 vanilla Draugr (cap-clamped). Suppressed during Undying Surge."),
            ("Wet Touch", "2 s", "In Death Throes + player within 3 m",
             "Applies vanilla Wet SE"),
            ("Death Throes", "One-shot", "HP ≤ 25%",
             "Speed x1.5. Aura Light shifts to blood-red at higher intensity. "
             "Activation burst (blob VFX + corpse explosion + red lightning ring of 8). "
             "Continuous red-lightning pulse every 0.6 s. "
             "Wet Touch activates: applies Wet to nearby player every 2 s."),
        ],
        "drops": [
            "TrophyDraugrEliteLord ×1 (one per player)",
            "Iron ×2–4",
            "Coal ×5–10",
            "BoneFragments ×5–10",
            "Entrails ×3–5",
        ],
        "trophy": "TrophyDraugrEliteLord (sickly-green retint of TrophyDraugrElite)",
        "blessing": {
            "name": "Iron Vein",
            "se": "SE_DraugrLordSpirit",
            "tooltip": "Mining iron from Swamp ores has a chance to yield an extra piece.",
        },
        "power": {
            "name": "Plague Bearer",
            "gp": "GP_PlagueBearer",
            "tooltip": "Full poison immunity. Outgoing poison damage +75%. "
                       "+50% health regeneration rate.",
        },
    },
    {
        "id": "fenring_lord",
        "name": "Fenring Lord",
        "biome": "Mountain",
        "tier": 4,
        "prefab": "FenringLord",
        "base_prefab": "Fenring",
        "hp": 7500,
        "scale": 1.4,
        "aura": "Pale blue → crimson (Blood Frenzy)",
        "intrinsic_dmg": 1.0,
        "kill_targets": ["Wolf", "Fenring", "Fenring_Cultist"],
        "default_kill_req": 20,
        "resistances": {
            "weak":      ["Fire"],
            "resistant": ["Frost"],
            "immune":    [],
            "note":      "Blunt, Slash, Pierce, Lightning, Poison Normal.",
        },
        "attacks": [
            ("Claw (light)",  0, 85,  0, 0, 0, 0, 0, "V", "AI", "Vanilla melee"),
            ("Claw (heavy)",  0, 95,  0, 0, 0, 0, 0, "V", "AI", "Vanilla melee"),
            ("Jump Attack",   0, 95,  0, 0, 0, 0, 0, "V", "AI", "Vanilla melee; AI-prioritized in Blood Frenzy"),
            ("Scream",        0,  0,  0, 0, 0, 0, 0,  0,  "AI", "Intimidate; no damage"),
            ("Bat Summon",                0,   0,   0,  0,  0,  0, 0, 0,   "60 s",  "2 Bats, max 4 nearby; Taunt anim tell"),
            ("Vampiric Strike ≤80% HP",   0,   0,   0,  0,  0,  0, 0, 0,   "40 s",  "60 s buff; heals 80% of dmg dealt per hit; off in Frenzy"),
            ("Shadow Fade ≤60% HP",       0,   0,   0,  0,  0,  0, 0, 0,   "60 s",  "Taunt + Bat Summon, 1 s delay, 12 s invisible"),
            ("Blood Frenzy  ≤30% HP",    0,   0,   0,  0,  0,  0, 0, 0,   "once",  "+50% spd, crimson aura, jump attacks heavily favored"),
        ],
        "abilities": [
            ("Bat Summon", "60 s", "Cap-gated (max 4 Bats within 16 m)",
             "Plays Taunt animation tell, then spawns up to 2 Bats (cap-clamped)"),
            ("Vampiric Strike", "40 s (after buff ends)", "HP ≤ 80%; not Blood Frenzy",
             "60 s buff: each landed melee hit heals 80% of the post-armor damage dealt; "
             "fx_GP_Activation VFX + blood-red aura on trigger"),
            ("Shadow Fade", "60 s", "HP ≤ 60%; player within 20 m",
             "Plays Taunt + spawns Bats, then after a 1 s delay turns invisible for 12 s "
             "(renderers hidden, particle/footstep FX stay visible as a tracking hint)"),
            ("Blood Frenzy", "One-shot", "HP ≤ 30%",
             "+50% speed, crimson aura, MonsterAI jump interval ×0.20 / attack interval ×0.30; "
             "jump-attack item's AI interval cut to 15% and prioritized, all other attacks ×5"),
        ],
        "drops": [
            "TrophyFenringLord ×1 (one per player)",
            "WolfFang ×3–5",
            "WolfPelt ×2–4",
            "FreezeGland ×1–2",
            "Silver ×1–2",
        ],
        "trophy": "TrophyFenringLord (pale-blue retint of TrophyFenring)",
        "blessing": {
            "name": "Pack Whisperer",
            "se": "SE_FenringLordSpirit",
            "tooltip": "Purely defensive/breeding gift. Tamed wolves within 30 m take −50% "
                       "damage, and tamed creatures breed/tame 2× faster while the bearer is "
                       "nearby. (Does NOT grant +damage dealt by wolves — that buff belongs "
                       "to Howl of the Pack below.)",
        },
        "power": {
            "name": "Howl of the Pack",
            "gp": "GP_HowlOfThePack",
            "tooltip": "On activation: every tamed creature within 30 m is fully healed and "
                       "marked with the pack's ember light. A ghostly violet Phantom Wolf "
                       "(non-interactable, follows the player) fights at your side for 60 s. "
                       "Any tamed Wolf within 30 m deals +100% damage for 10 minutes.\n\n"
                       "Synergy with Pack Whisperer: if the blessing is active when F is "
                       "pressed, tamed wolves take −85% damage (instead of −50%), the "
                       "Phantom Wolf endures for 2 minutes and ignores all incoming damage, "
                       "and Blood Magic skill scales the pack (50+ summons 2 wolves, 100 "
                       "summons 3).",
        },
    },
    {
        "id": "lox_lord",
        "name": "Lox Lord",
        "biome": "Plains",
        "tier": 5,
        "prefab": "LoxLord",
        "base_prefab": "Lox",
        "hp": 10000,
        "scale": 1.25,
        "aura": "Dusty yellow → crimson (Rage)",
        "intrinsic_dmg": 1.0,
        "kill_targets": ["Lox"],
        "default_kill_req": 10,
        "resistances": {
            "weak":      ["Pierce"],
            "resistant": [],
            "immune":    [],
            "note":      "Blunt, Slash, Fire, Frost, Lightning, Poison Normal.",
        },
        "attacks": [
            ("Lox Bite",  0, 130, 0, 0, 0, 0, 0, "V", "AI", "Vanilla melee"),
            ("Lox Slap",  120,  0, 0, 0, 0, 0, 0, "V", "AI", "Vanilla stomp"),
            ("Quake Stomp",           8,  0,  0,  0, 0, 0, 0,  50,   "12 s",  "5 m radial; ≤8 m trigger; 0.5 s tele"),
            ("Bull Rush",             8,  12, 0,  0, 0, 0, 0,  80,   "22 s",  "Charge over 0.8 s → 3.5 m impact"),
            ("Roaring Bellow ≤50% HP",6,  0,  0,  0, 0, 0, 0,  100,  "once",  "15 m radial blast"),
            ("Rage  ≤30% HP",         0,  0,  0,  0, 0, 0, 0,  0,    "once",  "+50% spd, crimson aura, cooldowns ×0.6"),
        ],
        "abilities": [
            ("Quake Stomp", "12 s", "Player within 8 m",
             "0.5 s telegraph → 5 m radial: 8 blunt + 50 push"),
            ("Bull Rush", "22 s", "Player within 6–22 m",
             "Lerps along target line over 0.8 s → 3.5 m impact: 12 slash + 8 blunt + 80 push"),
            ("Roaring Bellow", "One-shot", "HP ≤ 50%",
             "15 m radial: 6 blunt + 100 push + center message"),
            ("Rage", "One-shot", "HP ≤ 30%",
             "+50% speed, crimson aura, cooldowns ×0.6"),
        ],
        "drops": [
            "TrophyLoxLord ×1 (one per player)",
            "LoxMeat ×3–5",
            "LoxPelt ×2–4",
            "Barley ×8–15",
            "BlackMetalScrap ×1–3",
        ],
        "trophy": "TrophyLoxLord (golden retint of TrophyLox)",
        "blessing": {
            "name": "Hearth Master",
            "se": "SE_LoxLordSpirit",
            "tooltip": "Food buffs you eat last +100% longer (LordConfig.HearthMasterMultiplier, default 2.0×).",
        },
        "power": {
            "name": "Bull Rush",
            "gp": "GP_BullRush",
            "tooltip": "Cannot be staggered. Attacks cost −50% stamina. "
                       "Adrenaline surges +100% with every strike.",
        },
    },
    {
        "id": "seeker_lord",
        "name": "Seeker Lord",
        "biome": "Mistlands",
        "tier": 6,
        "prefab": "SeekerLord",
        "base_prefab": "Seeker",
        "hp": 12500,
        "scale": 1.4,
        "aura": "Violet → crimson (Hive Frenzy)",
        "intrinsic_dmg": 1.0,
        "kill_targets": ["Seeker", "SeekerBrute", "SeekerBrood"],
        "default_kill_req": 20,
        "resistances": {
            "weak":      ["Blunt"],
            "resistant": ["Slash"],
            "immune":    [],
            "note":      "Pierce, Fire, Frost, Lightning, Poison Normal.",
        },
        "attacks": [
            ("Claw Thrust",              0,   0, 120, 0,   0, 0, 0, "V", "AI", "Vanilla melee"),
            ("Acid Spit (Aerial)  ≤80% HP", 0, 0, 6, 0, 0, 0, 12, 25, "24 s",  "Below 80% HP. Takes off, hovers ~16 m out, faces player, scattered volleys; 3.5 m AoE each"),
            ("Burrow Ambush  ≤60% HP",  0,  0,   0,  0, 0, 0, 0,  50,   "22 s",  "Below 60% HP. Digs in, dashes underground, erupts under player; 4 m knock-up; 6–25 m trigger"),
            ("Brood Call",              0,  0,   0,  0, 0, 0, 0,  0,    "60 s",  "2 SeekerBrood, max 3 nearby"),
            ("Hive Frenzy  ≤30% HP",    0,  0,   0,  0, 0, 0, 0,  0,    "once",  "+50% spd, crimson aura, cooldowns ×0.6"),
        ],
        "abilities": [
            ("Acid Spit (Aerial)", "24 s", "HP ≤ 80%; player within 22 m + line of sight",
             "Takes off (Character.TakeOff flight), flies out to ~16 m standoff and "
             "rains glowing acid blobs — only while airborne and facing the player, "
             "with scattered (inaccurate) aim. Genuine vanilla acid launch/impact "
             "vfx + sfx. Each lands a 3.5 m AoE: 12 poison + 6 pierce + 25 push, then lands"),
            ("Burrow Ambush", "22 s", "HP ≤ 60%; on ground; player within 6–25 m + LoS",
             "0.6 s dig-in tell → vanishes (renderers hidden) and dashes underground "
             "toward the player → resurfaces and erupts beneath them with a 0.4 s "
             "telegraph, 4 m knock-up burst (50 push, upward)"),
            ("Brood Call", "60 s", "Cap-gated (max 3 SeekerBrood within 16 m)",
             "Spawns up to 2 SeekerBrood (cap-clamped)"),
            ("Hive Frenzy", "One-shot", "HP ≤ 30%",
             "+50% speed, crimson aura, all cooldowns ×0.6"),
        ],
        "drops": [
            "TrophySeekerLord ×1 (one per player)",
            "Carapace ×3–5",
            "Sap ×2–4",
            "Eitr ×1–3",
            "Mandible ×1–2",
        ],
        "trophy": "TrophySeekerLord (violet retint of TrophySeeker)",
        "blessing": {
            "name": "Refiner's Touch",
            "se": "SE_SeekerLordSpirit",
            "tooltip": "Smelter / Blast Furnace / Spinning Wheel / Eitr Refinery output near "
                       "you has a 50% chance (LordConfig.RefinersTouchChance) to drop a bonus item.",
        },
        "power": {
            "name": "Hive Sight",
            "gp": "GP_HiveSense",
            "tooltip": "For 10 minutes, every hostile creature within 80 m is marked on your "
                       "minimap and pulses with a faint sign — visible even through stone and mist.",
        },
    },
    {
        "id": "faller_valkyrie_lord",
        "name": "Fallen Valkyrie Lord",
        "biome": "Ashlands",
        "tier": 7,
        "prefab": "FallerValkyrieLord",
        "base_prefab": "FallenValkyrie",
        "hp": 25000,
        "scale": 1.3,
        "aura": "Radiant white-gold (brightens in Rage)",
        "intrinsic_dmg": 1.0,
        "kill_targets": ["Charred_Melee", "Charred_Archer", "Charred_Mage"],
        "default_kill_req": 25,
        "resistances": {
            "weak":      [],
            "resistant": [],
            "immune":    [],
            "note":      "Not modified by the mod — inherits vanilla Fallen Valkyrie resistances.",
        },
        "attacks": [
            ("Claw Strike",            0, 0, 160, 0, 0, 0, 0, "V", "AI", "Vanilla melee, unmodified"),
            ("Wind Gust Knockback",          0,  0,  0,  0, 0, 0, 0,  50,   "16 s",  "6 m radial; ≤7 m trigger; 0.4 s tele; push only, any HP"),
            ("Dive Bomb Slam  ≤80% HP",      0,  0,  0,  0, 0, 0, 0,  70,   "13 s",  "4 m AoE; ≤20 m; climb+dive; push only"),
            ("Soul Harvest  ≤30% HP",        0,  0,  0,  0, 0, 0, 0,   0,   "30 s",  "6 spirit/tick ×0.5 s for 6 s; ≤10 m; heals self equal to drain"),
            ("Rage  ≤30% HP",                0,  0,  0,  0, 0, 0, 0,   0,   "once",  "+50% spd, radiant aura, cooldowns ×0.6"),
        ],
        "abilities": [
            ("Wind Gust Knockback", "16 s", "Player within 7 m (any HP)",
             "0.4 s wing-spin telegraph → 6 m radial push: 50 push, blockable + dodgeable. "
             "Positioning tool — little to no direct damage, all knockback."),
            ("Dive Bomb Slam", "13 s", "HP ≤ 80%; player within 20 m",
             "Takes off, climbs ~10 m above the player's snapshotted position, telegraphs the "
             "strike point, then dives straight down. Impact: 4 m AoE, 70 push, blockable + "
             "dodgeable. Slam VFX fires on ground impact, not mid-air."),
            ("Soul Harvest", "30 s", "HP ≤ 30% to arm (locks out again above 50% HP); player within 12 m",
             "Hysteresis-armed burst heal: takes off and holds station airborne for 6 s, "
             "tethering to every player within 10 m and draining 6 spirit damage per tick "
             "(every 0.5 s, up to 12 ticks). Heals herself for the total drained each tick."),
            ("Rage", "One-shot", "HP ≤ 30%",
             "+50% speed, aura brightens to radiant white-gold, all ability cooldowns ×0.6, "
             "continuous spark pulse every 0.4 s."),
        ],
        "drops": [
            "TrophyFallerValkyrieLord ×1 (one per player)",
            "FlametalNew ×2–4",
            "Coal ×10–20",
            "CharredBone ×3–6",
            "SulfurStone ×4–8",
        ],
        "trophy": "TrophyFallerValkyrieLord (radiant white-gold retint of TrophyFallenValkyrie)",
        "blessing": {
            "name": "Featherweight",
            "se": "SE_FallerValkyrieLordSpirit",
            "tooltip": "The Fallen Valkyrie Lord's gift. Her wings bear your burdens: carry "
                       "up to 1000 weight with no encumbrance penalty (walk, run and recover "
                       "stamina freely until the cap), and gain +2 inventory rows. Switching "
                       "blessings spills the extra rows into a crate at your feet.",
        },
        "power": {
            "name": "Valkyrie's Rally",
            "gp": "GP_ValkyrieAscension",
            "tooltip": "The fallen Valkyrie answers your call to arms. Every player within "
                       "20 m — you and your kin — is restored in an instant: Health, Stamina "
                       "and Eitr filled to the brim; Adrenaline maxed if a trinket is equipped; "
                       "a max-level shield bubble, as from the Staff of Protection; and a "
                       "20-minute Rested buff.",
        },
        "notes": [
            "Wind Gust and Dive Bomb are push-only (no m_damage set on the HitData) — pure "
            "positioning/knockback tools. Soul Harvest is the fight's only true damage source, "
            "and it heals the Lord, not just drains the player.",
            "Valkyrie's Rally is a one-shot group restore burst, not a self-buff: the SE itself "
            "carries no stat mods and lapses after a 4 s InstantWindow — ValkyrieRallyService "
            "broadcasts a routed RPC so every nearby client restores itself (multiplayer-safe).",
            "Soul Harvest uses arm/disarm hysteresis (arms below 30% HP, disarms above 50% HP) "
            "so it can't immediately re-trigger the instant it ticks the Lord back over the line.",
        ],
    },
]


# Tier reference table used by the scaling system.
TIER_HP = {1: 500, 2: 2500, 3: 5000, 4: 7500, 5: 10000, 6: 12500, 7: 25000}
TIER_DMG_MULT = {1: 1.5, 2: 1.75, 3: 2.0, 4: 2.5, 5: 3.0, 6: 3.5, 7: 4.0}


# ---------------------------------------------------------------------------
# Styles
# ---------------------------------------------------------------------------

base_styles = getSampleStyleSheet()
title_style = ParagraphStyle(
    "TitleBig", parent=base_styles["Title"], fontSize=26, leading=30,
    alignment=TA_CENTER, textColor=colors.HexColor("#3a1f0d"),
    spaceAfter=18,
)
subtitle_style = ParagraphStyle(
    "Subtitle", parent=base_styles["Normal"], fontSize=12, leading=15,
    alignment=TA_CENTER, textColor=colors.HexColor("#5c3a18"),
    spaceAfter=24,
)
lord_name_style = ParagraphStyle(
    "LordName", parent=base_styles["Heading1"], fontSize=22, leading=26,
    textColor=colors.HexColor("#3a1f0d"), spaceBefore=8, spaceAfter=2,
)
lord_meta_style = ParagraphStyle(
    "LordMeta", parent=base_styles["Italic"], fontSize=11, leading=14,
    textColor=colors.HexColor("#666666"), spaceAfter=10,
)
section_h_style = ParagraphStyle(
    "SectionH", parent=base_styles["Heading2"], fontSize=13, leading=16,
    textColor=colors.HexColor("#2c5f8d"), spaceBefore=10, spaceAfter=4,
)
body_style = ParagraphStyle(
    "Body", parent=base_styles["Normal"], fontSize=10, leading=13,
    alignment=TA_LEFT, spaceAfter=4,
)
small_style = ParagraphStyle(
    "Small", parent=base_styles["Normal"], fontSize=8.5, leading=11,
    textColor=colors.HexColor("#444444"),
)
dmg_label_style = ParagraphStyle(
    "DmgLabel", parent=base_styles["Normal"], fontSize=7.5, leading=10,
    textColor=colors.HexColor("#333333"),
)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _eff_mult(lord):
    """Pre-compute effective damage multiplier for this lord."""
    return TIER_DMG_MULT[lord["tier"]] * lord["intrinsic_dmg"]


def _fmt_dmg(val, mult):
    """Format a damage cell value.
    0/None  → '—'
    'V'     → 'vanilla×N'  (inherits base prefab, scaled)
    'SE'    → 'SE'
    str     → pass through as-is (e.g. '5/s†')
    int     → 'base (→ eff)'
    """
    if val is None or val == 0:
        return "—"
    if val == "V":
        return f"van.\n×{mult:.2f}"
    if val == "SE":
        return "SE"
    if isinstance(val, str):
        return val
    eff = int(round(val * mult))
    return f"{val}\n(→{eff})"


def _fmt_push(val):
    """Push is always coded ×1.5 by LordDamageBoostPatch."""
    if val is None or val == 0:
        return "—"
    if val == "V":
        return "van.\n×1.5"
    eff = int(round(val * 1.5))
    return f"{val}\n(→{eff})"


# ---------------------------------------------------------------------------
# Builders
# ---------------------------------------------------------------------------

def cover_page(story):
    story.append(Spacer(1, 1.5 * inch))
    story.append(Paragraph("BiomeLords", title_style))
    story.append(Paragraph("Lord Handbook", title_style))
    story.append(Spacer(1, 0.4 * inch))
    story.append(Paragraph(
        f"Companion reference for the BiomeLords Valheim mod.<br/>"
        f"Mod version {MOD_VERSION} &nbsp;·&nbsp; Updated {GENERATED}",
        subtitle_style,
    ))
    story.append(Spacer(1, 0.4 * inch))
    story.append(Paragraph(
        "Each of seven biomes has a Lord — a boss-tier variant built from vanilla assets. "
        "Hunt the right creatures to earn the right to summon one, defeat it to claim its "
        "Forsaken Power, and mount its trophy on a Lord's Pedestal to call upon its blessing.",
        body_style,
    ))
    story.append(PageBreak())


def overview_page(story):
    story.append(Paragraph("Design overview", lord_name_style))
    story.append(Paragraph(
        "Lords are summoned with a <b>Lord's Horn</b> consumed at night in the matching "
        "biome, after enough qualifying creatures of that biome have been killed. "
        "On a successful summon, the world event for that Lord fires (forced weather + "
        "boss music) and the Lord is spawned 8 m ahead of the player. The horn is consumed.",
        body_style,
    ))
    story.append(Spacer(1, 8))
    story.append(Paragraph("Tier reference", section_h_style))
    rows = [["Tier", "Biome example", "Base HP", "Dmg mult"]]
    biome_for_tier = {1: "Meadows", 2: "Black Forest", 3: "Swamp",
                      4: "Mountain", 5: "Plains",
                      6: "Mistlands", 7: "Ashlands"}
    for t in range(1, 8):
        rows.append([str(t), biome_for_tier[t], str(TIER_HP[t]), f"×{TIER_DMG_MULT[t]:.2f}"])
    tbl = Table(rows, colWidths=[0.6 * inch, 2.4 * inch, 1.0 * inch, 1.1 * inch])
    tbl.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#2c5f8d")),
        ("TEXTCOLOR",  (0, 0), (-1, 0), colors.white),
        ("FONTNAME",   (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE",   (0, 0), (-1, -1), 9),
        ("GRID",       (0, 0), (-1, -1), 0.5, colors.HexColor("#bbbbbb")),
        ("ALIGN",      (0, 0), (-1, -1), "LEFT"),
        ("VALIGN",     (0, 0), (-1, -1), "MIDDLE"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1),
         [colors.HexColor("#f6efe4"), colors.white]),
    ]))
    story.append(tbl)
    story.append(Spacer(1, 14))
    story.append(Paragraph("Scaling formula at spawn", section_h_style))
    story.append(Paragraph(
        "<b>HP tier</b> = max(lordTier, playerHighestBossTier)<br/>"
        "<b>Damage tier</b> = max(lordTier, min(playerHighestBossTier, lordTier + 1))<br/>"
        "<b>Final damage mult</b> = TierTable[dmgTier] × LordConfig.DamageMultiplier × LordIntrinsic<br/>"
        "<b>Push force</b> = codedPush × 1.5 (always, LordDamageBoostPatch)",
        body_style,
    ))
    story.append(Spacer(1, 10))
    story.append(Paragraph(
        "<i>HP scales fully with player progression so veterans get a meaty fight from "
        "early-biome Lords. Damage is capped at lordTier + 1 so even Yagluth-tier "
        "players can't be one-shot by a Meadows Lord.</i>",
        small_style,
    ))
    story.append(PageBreak())


def resistances_table(lord):
    res = lord["resistances"]
    weak_str      = ", ".join(res["weak"])      or "—"
    resistant_str = ", ".join(res["resistant"]) or "—"
    immune_str    = ", ".join(res["immune"])    or "—"
    rows = [
        ["Weak",      "Resistant",      "Immune / V.Resistant"],
        [weak_str,    resistant_str,    immune_str],
    ]
    tbl = Table(rows, colWidths=[1.8 * inch, 1.8 * inch, 2.8 * inch])
    tbl.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#c0392b")),
        ("TEXTCOLOR",  (0, 0), (-1, 0), colors.white),
        ("FONTNAME",   (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE",   (0, 0), (-1, -1), 9),
        ("GRID",       (0, 0), (-1, -1), 0.5, colors.HexColor("#bbbbbb")),
        ("VALIGN",     (0, 0), (-1, -1), "MIDDLE"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.HexColor("#fdf0ee")]),
        ("LEFTPADDING",  (0, 0), (-1, -1), 5),
        ("TOPPADDING",   (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 3),
    ]))
    return tbl


def damage_table(lord):
    mult = _eff_mult(lord)
    hdr = ["Attack", "Blunt", "Slash", "Pierce", "Fire", "Frost", "Light.", "Poison", "Push", "CD", "Notes"]
    rows = [hdr]
    for atk in lord["attacks"]:
        name, blunt, slash, pierce, fire, frost, lightning, poison, push, cd, notes = atk
        rows.append([
            Paragraph(f"<b>{name}</b>", dmg_label_style),
            Paragraph(_fmt_dmg(blunt,     mult), dmg_label_style),
            Paragraph(_fmt_dmg(slash,     mult), dmg_label_style),
            Paragraph(_fmt_dmg(pierce,    mult), dmg_label_style),
            Paragraph(_fmt_dmg(fire,      mult), dmg_label_style),
            Paragraph(_fmt_dmg(frost,     mult), dmg_label_style),
            Paragraph(_fmt_dmg(lightning, mult), dmg_label_style),
            Paragraph(_fmt_dmg(poison,    mult), dmg_label_style),
            Paragraph(_fmt_push(push),           dmg_label_style),
            Paragraph(cd,                        dmg_label_style),
            Paragraph(notes,                     dmg_label_style),
        ])
    col_w = [1.4, 0.42, 0.42, 0.42, 0.42, 0.42, 0.42, 0.42, 0.52, 0.52, 1.72]
    tbl = Table(rows, colWidths=[w * inch for w in col_w])
    tbl.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#1a6e40")),
        ("TEXTCOLOR",  (0, 0), (-1, 0), colors.white),
        ("FONTNAME",   (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE",   (0, 0), (-1, 0), 7.5),
        ("GRID",       (0, 0), (-1, -1), 0.4, colors.HexColor("#bbbbbb")),
        ("VALIGN",     (0, 0), (-1, -1), "TOP"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1),
         [colors.HexColor("#edf7f1"), colors.white]),
        ("LEFTPADDING",  (0, 0), (-1, -1), 3),
        ("RIGHTPADDING", (0, 0), (-1, -1), 3),
        ("TOPPADDING",   (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 3),
    ]))
    return tbl


def ability_table(abilities):
    rows = [["Ability", "Cooldown", "Trigger", "Effect"]]
    for ab in abilities:
        rows.append([
            Paragraph(f"<b>{ab[0]}</b>", small_style),
            Paragraph(ab[1], small_style),
            Paragraph(ab[2], small_style),
            Paragraph(ab[3], small_style),
        ])
    tbl = Table(rows, colWidths=[1.2 * inch, 0.75 * inch, 1.7 * inch, 2.95 * inch])
    tbl.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#2c5f8d")),
        ("TEXTCOLOR",  (0, 0), (-1, 0), colors.white),
        ("FONTNAME",   (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE",   (0, 0), (-1, 0), 9),
        ("GRID",       (0, 0), (-1, -1), 0.5, colors.HexColor("#bbbbbb")),
        ("VALIGN",     (0, 0), (-1, -1), "TOP"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1),
         [colors.HexColor("#f6efe4"), colors.white]),
        ("LEFTPADDING",  (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING",   (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 4),
    ]))
    return tbl


def lord_page(story, lord):
    mult = _eff_mult(lord)

    story.append(Paragraph(lord["name"], lord_name_style))
    story.append(Paragraph(
        f"Tier {lord['tier']} &nbsp;·&nbsp; {lord['biome']} &nbsp;·&nbsp; "
        f"Prefab <b>{lord['prefab']}</b> &nbsp;·&nbsp; Cloned from <i>{lord['base_prefab']}</i>",
        lord_meta_style,
    ))

    # Stats
    story.append(Paragraph("Stats", section_h_style))
    stats_rows = [
        ["Base HP", str(lord["hp"]),
         "Scale", f"×{lord['scale']:.2f}"],
        ["Aura", lord["aura"],
         "Intrinsic dmg mult", f"×{lord['intrinsic_dmg']:.2f}"],
        ["Kill targets", ", ".join(lord["kill_targets"]),
         "Kills required", str(lord["default_kill_req"])],
        ["Tier dmg mult", f"×{TIER_DMG_MULT[lord['tier']]:.2f}",
         "Effective dmg mult", f"×{mult:.3f}"],
    ]
    stats_tbl = Table(stats_rows, colWidths=[1.2 * inch, 2.3 * inch, 1.3 * inch, 1.8 * inch])
    stats_tbl.setStyle(TableStyle([
        ("FONTSIZE",  (0, 0), (-1, -1), 9),
        ("FONTNAME",  (0, 0), (0, -1), "Helvetica-Bold"),
        ("FONTNAME",  (2, 0), (2, -1), "Helvetica-Bold"),
        ("TEXTCOLOR", (0, 0), (0, -1), colors.HexColor("#2c5f8d")),
        ("TEXTCOLOR", (2, 0), (2, -1), colors.HexColor("#2c5f8d")),
        ("VALIGN",    (0, 0), (-1, -1), "TOP"),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
    ]))
    story.append(stats_tbl)

    # Resistances
    story.append(Paragraph("Resistances (inherited from vanilla prefab)", section_h_style))
    story.append(resistances_table(lord))
    if lord["resistances"].get("note"):
        story.append(Paragraph(lord["resistances"]["note"], small_style))

    # Damage table
    story.append(Paragraph(
        f"Attack damage — base values (→ effective after ×{mult:.3f}); push always ×1.5",
        section_h_style,
    ))
    story.append(damage_table(lord))
    story.append(Paragraph(
        "SE = status-effect only (no direct damage). "
        "van. = vanilla-inherited value. "
        "† Plague Cloud bypasses Lord dmg mult — value is fixed.",
        small_style,
    ))

    # Abilities
    story.append(Paragraph("Abilities (detail)", section_h_style))
    story.append(ability_table(lord["abilities"]))

    # Drops
    story.append(Paragraph("Drops on death", section_h_style))
    for d in lord["drops"]:
        story.append(Paragraph(f"&bull;&nbsp; {d}", body_style))

    # Trophy
    story.append(Paragraph("Trophy", section_h_style))
    story.append(Paragraph(lord["trophy"], body_style))

    # Blessing
    story.append(Paragraph("Blessing (Lord's Pedestal)", section_h_style))
    story.append(Paragraph(
        f"<b>{lord['blessing']['name']}</b> &nbsp;(<font color='#888'>{lord['blessing']['se']}</font>)",
        body_style,
    ))
    story.append(Paragraph(lord["blessing"]["tooltip"], body_style))

    # Forsaken Power
    story.append(Paragraph("Forsaken Power (F key)", section_h_style))
    story.append(Paragraph(
        f"<b>{lord['power']['name']}</b> &nbsp;(<font color='#888'>{lord['power']['gp']}</font>)",
        body_style,
    ))
    story.append(Paragraph(lord["power"]["tooltip"], body_style))

    # Optional notes
    if lord.get("notes"):
        story.append(Paragraph("Notes", section_h_style))
        for n in lord["notes"]:
            story.append(Paragraph(f"&bull;&nbsp; {n}", small_style))

    story.append(PageBreak())


def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(colors.HexColor("#888888"))
    canvas.drawString(
        0.75 * inch, 0.5 * inch,
        f"BiomeLords {MOD_VERSION} — Lord Handbook — generated {GENERATED}",
    )
    canvas.drawRightString(
        LETTER[0] - 0.75 * inch, 0.5 * inch,
        f"Page {doc.page}",
    )
    canvas.restoreState()


def build():
    out = Path(__file__).with_name("BiomeLords_Handbook.pdf")
    doc = SimpleDocTemplate(
        str(out),
        pagesize=LETTER,
        leftMargin=0.75 * inch, rightMargin=0.75 * inch,
        topMargin=0.6 * inch, bottomMargin=0.75 * inch,
        title=f"BiomeLords Lord Handbook {MOD_VERSION}",
        author="BiomeLords",
    )
    story = []
    cover_page(story)
    overview_page(story)
    for lord in LORDS:
        lord_page(story, lord)
    doc.build(story, onFirstPage=footer, onLaterPages=footer)
    print(f"Wrote {out}")


if __name__ == "__main__":
    build()
