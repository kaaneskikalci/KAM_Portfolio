# KAM — Design Case Study

**KAM** is an isometric, turn-based roguelite RPG set in Turkish mythology. You play
Göktay, the shaman (*kam*) of his Oba, who enters the Spirit Realm to recover a soul
taken by evil spirits. You sneak past or fight spirits in turn-based battles where
timing matters as much as choice, then return to the village to grow stronger.

**Genre:** turn-based roguelite RPG · **Platform:** PC — `TODO(Kaan): confirm platform(s)` ·
**Tech:** Unity 6 (6000.4), C#, URP, Input System, Cinemachine
**Context:** graduation project, Istanbul Bilgi University · team of five · two semesters

- **Full case study:** https://kaaneskikalci.com/dev/kam
- **Play it:** https://kaaneskikalci.itch.io/kam

---

## Media

> `TODO(Kaan):` add media to `docs/media/` and replace these placeholders.

| | |
|---|---|
| `TODO(Kaan): GIF — a timed-hit rune prompt landing a Perfect` | `TODO(Kaan): GIF — banking AP with Defend, then spending it` |
| `TODO(Kaan): screenshot — the Oba hub and skill-tree altar` | `TODO(Kaan): screenshot — exploration, enemy vision cone` |

## Design goals and pillars

> `TODO(Kaan):` replace with the pillars from the GDD. The draft below is inferred
> from how the systems are built, not quoted from a design document.

1. **Skill expression inside turn-based combat.** Turns decide *what* you do; a
   timed directional input decides *how well* it lands.
2. **Readable, fair decisions.** Enemy intent, turn order and damage previews are on
   screen, and an action that can't execute costs nothing.
3. **Mythology as mechanics.** Skills are named after and themed on figures from Turkic
   myth — Umay Ana, Erlik Kaan, Ilgen, the ancestors.
4. **A world that remembers.** Bonfires, opened shortcuts and defeated bosses persist,
   so the Spirit Realm you have opened up stays open between deaths.

## Core loop

**Run structure.** Oba (hub) → Spirit Realm portal → explore, loot, fight → rest at
bonfires → die or return → spend Spirit Fragments on the skill tree → go again.

- Temporary items are cleared when a run starts and when you return to the Oba;
  Spirit Fragments and skill unlocks are kept.
- On death you respawn at the last bonfire and lose **50% of carried Spirit
  Fragments**. With no bonfire saved, you fall back to the Oba.

**Turn structure.** Combat starts when a roaming enemy spots you; nearby enemies join.
Turn order is recalculated every round from effective speed, and some enemies charge
up and act only every *N* turns. On the player's turn:

- Gain **1 AP**; up to **2 AP** can be banked.
- Basic Attack costs 1 AP and triggers a timed-hit rune.
- **Defend is free**: it raises a shield and keeps your AP for next turn.
- Bigger skills, such as Echo of Ancestors (2 AP), need a banked turn first.

**Progression.** Spirit Fragments drop from enemies, chests and pickups. You spend
them at the Skill Tree Altar in the Oba. The tree has 10 skills in five tiers, ending
in an ultimate, and four of the skills make you choose one of two branches.

## Key systems

### 1. Timed-hit runes
A directional rune appears during an attack: press the matching arrow in the window
for **Success (×1.3)**, or at its centre for **Perfect (×1.5)**. After three hits in a
row the window shrinks toward a minimum, so streaks get harder. This keeps a
menu-driven fight from becoming automatic. Tuning is a ScriptableObject, not code.
→ [`Combat/TimedHit/`](Assets/Scripts/Combat/TimedHit) ·
[`TimedHitConfig.asset`](Assets/ScriptableObjects/TimedHitConfig.asset)

### 2. Action Points and the free Defend
Defend costs no AP, so it banks AP for next turn: give up damage now to afford a bigger
move later. Underneath is one rule: **a failed action must not spend AP or end the
turn** — execution is validated before anything is consumed.
→ [`CombatAction.cs`](Assets/Scripts/Combat/Actions/CombatAction.cs) ·
[`DefendAction.cs`](Assets/Scripts/Combat/Actions/DefendAction.cs) ·
[`PlayerStats.cs`](Assets/Scripts/Core/Stats/PlayerStats.cs)

### 3. Readable turn order and enemy intent
Speed sets turn order and buffs/debuffs re-sort it. Enemies show their next intent and
the HUD previews damage, so combat is planned, not guessed. Combos reward consecutive
attacks and break when you take damage or defend.
→ [`TurnSystem/`](Assets/Scripts/Combat/TurnSystem) ·
[`EnemyIntent.cs`](Assets/Scripts/Core/Combat/EnemyIntent.cs) ·
[`ComboSystem.cs`](Assets/Scripts/Combat/ComboSystem.cs) ·
[`UI/Combat/`](Assets/Scripts/UI/Combat)

### 4. Branching skill tree as a data layer
Each skill is a `SkillNode` asset with cost, tier, prerequisites and optional
branches (e.g. Rune Power-up → *Blinding* or *Life Steal*). Systems don't hardcode
bonuses; they ask a central `SkillEffectApplier` whether an effect is active, so adding
a skill doesn't mean editing combat. Some skills reach into exploration: Erlik Kaan's
Cloak shrinks enemy vision, Eye for Nature makes collectibles glow.
→ [`Progression/`](Assets/Scripts/Progression) ·
[`ScriptableObjects/Skills/`](Assets/ScriptableObjects/Skills)

### 5. Bonfires and a persistent world
Bonfires save and heal. Opened shortcuts and defeated bosses persist through a shared
`ISaveable` interface: death costs Fragments, but the map you opened stays open.
→ [`SaveSystem/`](Assets/Scripts/SaveSystem) ·
[`PersistentShortcut.cs`](Assets/Scripts/Interaction/Interactables/PersistentShortcut.cs)

## My role

Team of five. **I was the only developer** — every gameplay system in this repository
is mine — and I shared design work with the team. Teammates made art, narrative and
audio.

> `TODO(Kaan):` which design decisions were yours (timed hits, AP banking, skill tree,
> death penalty?), design-process work (GDD sections, playtests, balancing), project
> dates, and teammates' names/roles if you want to credit them.

## Status and lessons learned

> `TODO(Kaan):` project status (final graduation build? still in development?),
> playtest findings/results, and what you learned as a designer — what changed after
> playtesting, what you would cut or redo.

## Repository guide

A **code-only mirror**: C# source, ScriptableObject data and project settings. Art,
audio, animation, scenes and prefabs belong to teammates or third parties and are not
included, so it won't open as a playable project — play via itch.io. The GDD isn't
here, but code comments cite its sections (e.g. *GDD 1.7.5*).

| Path | Contents |
|---|---|
| `Assets/Scripts/Combat` | Turn manager, turn order, actions, combos, timed-hit runes |
| `Assets/Scripts/Core` | Game state (hub ↔ run), combat units, stats, status effects, enemy intent |
| `Assets/Scripts/Progression` | Skill tree data, unlock state, skill-effect query layer |
| `Assets/Scripts/Hub` | Oba hub, Spirit Realm portal, skill-tree altar |
| `Assets/Scripts/SaveSystem` | Bonfire saves, death penalty, persistent world state |
| `Assets/Scripts/Inventory` | Inventory, quick slots, loot tables, items, plants |
| `Assets/Scripts/Interaction` | Interactables: chests, lore objects, shortcuts |
| `Assets/Scripts/KaanEnemyandBattle1` | Enemy roaming, vision, state machine, battle entry |
| `Assets/Scripts/KaanMovementAndCamera1` | Player movement, isometric camera |
| `Assets/Scripts/UI` | Combat HUD, skill tree, inventory, menus, feedback |
| `Assets/Scripts/Editor` | Editor setup helpers for combat, UI, skill tree, inventory |
| `Assets/ScriptableObjects` | Skill, item, plant and timed-hit data |

## Licence

MIT — see [LICENSE](LICENSE). Applies to the source in this repository only.
