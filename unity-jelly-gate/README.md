# CROWNFRONT — Unity native v2.2.2

This is a native Unity 6 project, not an HTML/WebView wrapper.

## What is implemented

- Centered castle battlefield with three broad, clearly separated hill roads
- Three independent curved enemy routes that converge naturally before the castle gate
- Wriggling enemy locomotion, spacing, body blocking, and gate attacks
- Gate HP (600), no automatic round-clear healing, and persistent attacks from enemies that reach the gate
- Five tactical unit roles: shield, melee, archer, area mage, single-target mage
- Four-frame directional walking and three-stage attacks for every defender, with separate down, side, and rear views
- Direction-normalized sprite scale and foot baselines so rear-facing motion matches front and side proportions
- Dedicated full-art formation icons with aspect-safe fitting and no adjacent animation-cell bleed
- Six visually distinct level-5 hero animation sets, including a royal Clockwork Bombardier evolution
- Portrait main menu composed directly from the same live shield-hero and jelly animation sprites used in battle
- Manual role-specific ultimate abilities for all six hero evolutions
- Combat-time-only skill cooldowns that freeze during placement and restart only after an actual cast
- Clearance-aware A* movement, corner-safe diagonals, path smoothing, and automatic stuck recovery around rocks
- Micro-obstacle filtering so pebbles and tiny decorative terrain pixels do not block movement
- Physical attack and magic power as separate stats, with armor and magic resistance mitigation
- Active combat skills for every defender and enemy class
- High-ground targeting rules: melee enemies cannot attack defenders stationed on hills
- Repair augments and an unlockable sixth defender, the Clockwork Bombardier
- Polished toy-kingdom defender, jelly-enemy, and five level-5 hero-evolution sprites created for this project
- Role-specific combat motion: shield/melee wind-up and lunge, archer draw and recoil, mage lift and cast
- Physical armor and magic resistance for every defender and enemy
- Wider level power gaps with deliberately weaker early levels and a major level-5 hero spike
- Boss barrier, telegraphed ground-slam skill, knockback, enrage phase, and dedicated boss HUD
- Placement mode with cancel, immediate repositioning, drag box selection, group movement, and stop
- RTS-style focus fire: tapping an enemy makes selected units approach only to their maximum attack range
- Automatic target acquisition and pursuit inside a detection radius larger than attack range; Stop explicitly disables it
- Isolated animation frames that remove neighbouring-pose and next-row sprite bleed
- Decoration-free traversal over pebbles, shrubs, ruts, and narrow shadows while cliffs remain terrain-locked
- Visible hold-position badges and selected-unit hold status
- Rebalanced boss durability, defenses, attacks, barrier, and skill cadence
- Rebalanced experience curve: tanks level moderately while damage and magic units progress faster
- Visible arrows and magic bolts with trails, cast glows, melee slashes, impact sparks, area bursts, health bars, wave and boss logic
- Larger framed mobile HUD, centered segmented gate durability, and hideable full-size augment selection cards
- Safe-area-aware HUD placement for notches, punch-hole cameras, gesture bars, and rounded display corners
- Persistent gate attackers: enemies remain at the wall and repeatedly damage it until defeated
- No automatic gate healing between rounds; wall augments now reduce incoming damage instead
- Separate animated feet, movement acceleration, contact recoil, and role-specific action accents
- Expanded level-up and hero-evolution pillar, rings, rays, sparks, and floating labels
- Fresh budget each round while surviving units, positions, levels, and experience persist
- Damage-participation experience sharing and automatic hero evolution at level 5
- Stackable Bronze/Silver/Gold/Platinum/Diamond augments with exact numeric effects shown on every card
- Tier transition probabilities based on the previous augment, with boss-round high-tier bonuses
- Full-map presentation with compact floating round, coin, formation, augment, gate, and wave controls
- Authored three-road corridor navigation: artwork pixels, pebbles, shrubs, and shadows cannot form colliders or trap units; only the road corridors are traversable
- Static corridor integrity audit plus a live 48-plan, multi-bend traversal stress test for the three lanes
- Narrower road-only corridor colliders that reject visually adjacent hill shoulders while preserving all three road routes
- Same-tier augment rows, non-repeating recruit unlocks, an Emerald Lancer, a Petal Druid, and clearly displayed attack ranges
- Wave roster progression: jelly soldiers, skeletons, runners, brutes, shamans, siege golems, and bosses scale by round
- Slower hero progression with a larger early-level power gap
- Stackable active augments: Royal Flare, Time Lock, and Emergency Gate Repair
- Clean side-aligned main menu, Android Back system menu, settings, and procedural character battle barks
- Purple area mage basic attacks now trade low per-target damage for a large visible magic blast
- Two more augment recruits: Brass Musketeer (Gold, long physical range) and Moonlight Oracle (Diamond, magic area control)
- Separate procedural main-menu fanfare and battle-march music loops
- Every recruit, including the Brass Musketeer and Moonlight Oracle, evolves at level 5 with a hero title, four-direction hero animation, unique aura tint, and ultimate ability
- Narrower authored road corridors, shoulder blockers, and placement clearance rejection prevent units from being placed or moved onto visible hill interiors
- CROWNFRONT battlefield-backed menu, Korean/English language switch, and a six-item no-reward challenge collection
- New wave specials: armor-piercing attackers, physical-immune arcane wisps, and flying jellies that only ranged defenders can target

## Controls

- Tap a unit card, then tap the battlefield to place it.
- Tap the selected card again, the Cancel button, right-click, or Back to cancel placement.
- Drag from a unit to move the current selection immediately.
- Drag an empty area to box-select several units; then tap a destination to move the formation.
- Tap an enemy to make selected units pursue it and attack from their maximum range.
- Units automatically engage enemies inside their wider detection radius.
- Stop holds selected units in place but they still fire at enemies that enter attack range.
- Android Back opens a menu with Resume, Main Menu, Settings, and Quit.

## Open and build

1. Open the folder in Unity Hub with Unity `6000.0.34f1`.
2. Activate a Unity Personal license if Hub asks for one.
3. Install Android Build Support, SDK/NDK Tools, and OpenJDK for that editor.
4. Run `Jelly Gate > Configure Project`.
5. Run `Jelly Gate > Build Android APK`.

The APK is written to the parent `outputs/JellyGate-Unity.apk` folder by default.

The supplied APK is a 64-bit ARM Android build, uses Unity's non-development configuration, and is signed with an Android debug certificate. For store distribution, switch to a release keystore and build an Android App Bundle (AAB).

Batch build:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.34f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\unity-jelly-gate' `
  -executeMethod JellyGate.Editor.JellyGateBuild.BuildAndroid `
  -outputPath 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\outputs\JellyGate-Unity.apk' `
  -logFile 'C:\Users\Administrator\Documents\Codex\2026-07-22\new-chat\work\unity-android-build.log'
```
