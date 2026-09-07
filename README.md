# Unity 2D Action

[한국어 README](README_KR.md)

![Boss Battle](Images/BOSS.png)

A 2D action game developed as a solo Unity project.

The game is built around three weapons, four elemental paths, room-based progression, and upgrades that persist throughout a run.

> This is a code-focused portfolio repository.  
> Third-party art, audio, and font files are not included because of redistribution restrictions.

## Overview

| | |
|---|---|
| Engine | Unity 6000.3.11f1 (Unity 6) |
| Language | C# |
| Rendering | URP 2D |
| Platform | Windows x86_64 |
| Genre | 2D Action |
| Development | Solo Project |

## Gameplay

A run is divided into two chapters.

Each chapter contains three combat rooms followed by a boss fight.  
Enemies must be defeated before the exit opens and the player can move on to the next room.

The player can use three weapons:

- Sword — three-hit melee combo
- Bow — ranged single-shot attacks
- Dagger — four-hit fast combo with an additional Echo effect

At the beginning of a run, one element is selected:

- Fire
- Lightning
- Ice
- Wind

The selected element stays fixed for that run and can be upgraded along with the equipped weapon.

Defeating enemies gives experience, and leveling up opens a three-choice upgrade screen.

## Main Features

- Sword, Bow, and Dagger with separate combat styles
- Fire, Lightning, Ice, and Wind elemental progression
- Weapon-specific and element-specific upgrades
- Independent weapon levels
- Two chapters with normal combat rooms and boss encounters
- Scene-to-scene run state persistence
- Game Over, Retry, and Run Clear flows
- Boss HP UI
- Independent BGM and SFX volume settings
- Projectile object pooling
- Custom Unity Editor tools used during development

## Technical Highlights

### Run state between scenes

The game uses `RunManager` and `RunData` to keep the current run state while scenes change.

This includes:

- player HP and EXP
- equipped weapon
- Sword / Bow / Dagger levels
- selected element and element levels
- player stats
- acquired upgrade stacks

One issue I ran into was that some upgrades were being applied again whenever a new scene loaded.

I changed the restore flow so that saved stat values are restored first, while only effects that actually need to be recreated on scene components are applied again.

This prevents scene transitions from increasing the same upgrade more than once.

### Weapon and damage structure

Sword, Bow, and Dagger share the same base `Weapon` damage calculation.

The common damage multiplier and weapon-level multiplier are calculated once before the result is passed to each weapon's attack logic.

Attacks use `DamageData` to pass resolved damage, critical information, and element data to the target.

Secondary attacks such as WindSlash and Dagger Echo use the already calculated damage value instead of running the common multiplier again.

### Weapon animations

The three weapons use different attack animations, but I did not create a completely separate player animator for each weapon.

Movement, jumping, landing, hit reactions, and the basic attack state structure are shared.

Weapon-specific animation clips are swapped through Animator Override Controllers.

This keeps the player animation logic in one place while still allowing Sword, Bow, and Dagger to have different animation sets.

### Room progression

`RoomController` tracks enemies through `EnemyRoomMember`.

When the final tracked enemy dies, the room unlocks its `ExitDoor`.

I also reused the normal enemy death pipeline for enemies that fall outside the stage. This fixed a case where an enemy could fall out of reach without being counted as dead, leaving the room permanently locked.

Player falls work in a similar way: they use the normal `PlayerHealth` death event so the existing Game Over and Retry flow can be reused.

### Persistent Boss UI

`GlobalUI` remains alive between gameplay scenes.

This originally caused the boss HP UI to disappear because the scene-local UI hierarchy could be removed while the persistent UI already existed.

`BossHealthUI` is moved under the persistent canvas while still keeping track of the boss scene it belongs to.

During the fight, it refreshes the boss HP display and cleans itself up when that boss scene is unloaded.

### Audio between scenes

`BgmPlayer` is also persistent.

If two consecutive scenes use the same BGM track, playback continues from the current position instead of restarting from the beginning.

A fade is only performed when the requested track actually changes.

SFX and BGM volume settings are stored separately.

For a more detailed breakdown of the project structure:

[Architecture Documentation](Docs/ARCHITECTURE.md)

## Problems I Worked Through

### Upgrade values increasing after scene changes

**Problem:**  
Some persistent upgrades were applied again after entering a new room.

**Cause:**  
Saved stat values and upgrade effects were both being reconstructed during restore.

**Fix:**  
I separated snapshot restoration from effects that need to be reapplied to scene components.

**Result:**  
The player's build now keeps the same effective values across scene transitions.

---

### Boss HP bar disappearing

**Problem:**  
The boss HP bar could disappear when entering a boss scene.

**Cause:**  
The project already had a persistent `GlobalUI`, so the duplicate scene UI was removed along with the boss UI attached to it.

**Fix:**  
I moved the boss UI to the persistent canvas before the duplicate UI was removed and kept the original boss scene information for cleanup.

**Result:**  
The boss HP bar works in both boss rooms and is removed when the boss scene ends.

---

### Fallen enemies blocking room completion

**Problem:**  
An enemy could fall outside the stage and become impossible to kill, so the exit never opened.

**Cause:**  
The enemy never entered the normal death flow and was still counted as alive.

**Fix:**  
The fall kill zone now calls the same `EnemyHealth` death pipeline used by normal combat.

**Result:**  
Drops, room enemy counts, and exit unlocking all continue to work through the same system.

---

### Supporting three weapon animation sets

**Problem:**  
The weapons needed different attack animations, but duplicating the full player animator for every weapon would make maintenance harder.

**Fix:**  
I kept one shared animator structure and used Animator Override Controllers for the weapon-specific clips.

**Result:**  
Movement logic stays shared while each weapon can use its own visual set.

## Project Structure

```text
Assets/
├─ Scripts/             # Runtime gameplay code
├─ Editor/              # Project-specific Unity Editor tools
└─ Data/
   └─ Upgrades/         # UpgradeData assets and database

Packages/               # Unity package manifest
Docs/                   # Architecture documentation and media
```

### Script Areas

| Folder | Responsibility |
|---|---|
| `Audio` | BGM, SFX, UI audio, and volume settings |
| `Boss` | Boss health, attacks, projectiles, and boss-specific logic |
| `Camera` | Player-follow camera behavior |
| `Combat` | Shared damage data, critical hits, and combat handling |
| `Core` | Reusable runtime infrastructure and object pooling |
| `Enemy` | Enemy AI, health, attacks, room tracking, and fall handling |
| `Item` | Experience gems and collectible behavior |
| `Player` | Movement, health, status, animation, and player state |
| `Run` | Cross-scene run state and chapter progression |
| `UI` | HUD, pause, level-up, element selection, status, boss, and result UI |
| `Upgrade` | Upgrade filtering, application, stacking, and restoration |
| `Utility` | Shared helper components |
| `Weapon` | Sword, Bow, Dagger, projectiles, hitboxes, and secondary attacks |

## Controls

| Input | Action |
|---|---|
| `A` / `D` or Arrow Keys | Move |
| `Space` | Jump / Interact |
| `Z` | Attack |
| `S` | Open / Close Status |
| `Esc` | Pause / Back |

## Development Environment

- Unity 6000.3.11f1
- C#
- Universal Render Pipeline 2D
- Unity Input System
- Windows Standalone x86_64

## Development Workflow

AI-assisted development tools were used for parts of the project, mainly to speed up code drafting, repetitive Unity Editor tooling, and project-wide validation.

I defined the system requirements and integration constraints, then reviewed, adapted, and tested the generated code within the project.

Final gameplay behavior and technical decisions were verified through direct testing.

## Build Status

- Windows Release Build: **Succeeded**
- Runtime compile errors: **0**
- Editor compile errors: **0**
- Missing scripts in the final audit: **0**
- Full Play Mode run-through: **Completed**

## Screenshots / Video

### Gameplay

![Gameplay](Images/Gameplay.png)

### Upgrade Selection

![Upgrade Selection](Images/LevelUp.png)

### Element Selection

![Element Selection](Images/ElementSelect.png)

Gameplay video will be added later.

## Third-party Assets

This project uses third-party art, audio, and font assets during development.

Those binary files are intentionally excluded from this public repository.

Licensing and attribution information is available in:

[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

## Repository Notice

This repository is intended for portfolio and code review purposes.

Because several third-party assets are not redistributed as part of this public repository, cloning this repository alone does **not** produce the complete playable Unity project.

The repository focuses on first-party C# gameplay code, custom Unity Editor tools, upgrade data, package configuration, and technical documentation.