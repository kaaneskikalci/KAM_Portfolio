# KAM — gameplay source

**KAM** is an isometric turn-based roguelite RPG set in Turkish mythology, built in
Unity as a graduation project at Istanbul Bilgi University. You play Göktay, the
shaman of his Oba, who enters the Spirit Realm to recover a soul the evil spirits
took.

Five of us made it across two semesters. **I was the only developer on the team** —
every gameplay system in this repository is mine, alongside shared design work on the
game itself. Art, narrative and audio were done by the rest of the team.

- **Case study:** https://kaan-portfolio-three.vercel.app/dev/kam
- **Play it:** https://kaaneskikalci.itch.io/kam

---

## What this repository is

This is a **code-only mirror**. It contains the C# source and the ScriptableObject
data that drive the game, plus the project settings — nothing else.

It does **not** contain art, audio, animations, scenes or prefabs. Those were made by
my teammates or come from third-party sources, and they are not mine to redistribute.
So this will not open as a playable Unity project, and it is not meant to: it is here
to be read, not built. If you want to play the game, use the itch.io link above.

## Where to look

| Path | What lives there |
|---|---|
| `Assets/Scripts/Combat` | Turn manager, `CombatUnit`, Action Point system, timed-hit resolution |
| `Assets/Scripts/Core` | Game flow, scene transitions, run/hub state |
| `Assets/Scripts/Progression` | Skill tree and the central skill-effect query layer |
| `Assets/Scripts/Inventory` | Inventory, quick slots, pickups, spirit fragments |
| `Assets/Scripts/Interaction` | Shared interaction flow — chests, bonfires, portals, altar |
| `Assets/Scripts/SaveSystem` | Bonfire save, death/respawn, persistent world state |
| `Assets/Scripts/UI` | Combat HUD, tooltips, damage popups, combat log, menus |
| `Assets/ScriptableObjects` | Item, skill and enemy data — the reason adding content did not mean editing combat |

Two decisions the code is built around, if you only read a little of it:

- **A failed action must not spend AP or end the turn.** The system checks whether an
  action can execute before it consumes anything, which is what made Defend a real
  tactical choice rather than a wasted turn.
- **Systems ask a central effect layer whether a skill bonus is active** instead of
  hardcoding bonuses into combat scripts. Adding a skill does not mean editing combat.

## Licence

MIT — see [LICENSE](LICENSE). Applies to the source in this repository only.
