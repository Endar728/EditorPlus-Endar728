# Changelog

All notable changes to this project are documented in this file.

## [1.6.3] - 2026-05-29

### Fixed

- **Atomic Builder Library tab** — Restored the **Library** tab and blueprint file browser that were removed in 1.6.2. Browse saved blueprints under `<game install>/Blueprints/`, filter by name, and click to select for paste.

## [1.6.2] - 2026-05-28

### Added

- **Duplicate in place** — **Tools → Duplicate** (or **Ctrl+D**): copies the current selection with a small offset so copies don’t stack on the originals.
- **Batch rename** — **Tools → Batch rename…**: rename all selected units at once with a prefix and start number (`Squad_A_1`, `Squad_A_2`, …). Names are checked for uniqueness across the mission.

### Changed

- **Mission editor toolbar** — **Duplicate**, **Batch rename**, and **Graph grid** moved under a single **Tools** dropdown to reduce top-bar clutter. **Atomic Builder** label shortened to **Builder**.

### Fixed

- **Tools menu positioning** — Dropdown opens below the toolbar instead of overlapping other buttons.
- **Batch rename panel** — Panel moved to the bottom-right corner so it no longer blocks the left unit inspector.

## [1.6.1] - 2026-05-28

### Added

- **Atomic Builder panel** — Mission-editor **Builder** toolbar button; Blueprint and Library tabs; dark grey / green UI aligned with native editor.
- **Blueprint save** — Radius-based capture from live editor units; writes JSON under `<game folder>/Blueprints/` (folder auto-created on load).
- **Blueprint paste** — **Ctrl+Alt+V** at cursor; throttled spawn, formation terrain clamp, per-unit terrain snap when collision is on.
- **Blueprint library** — Lists `.json` files in the game `Blueprints` folder with search/filter.

### Changed

- **Ctrl+V** — Unit group paste only; **Ctrl+Alt+V** is blueprint paste (no hotkey conflict).
- **Blueprint storage** — `Nuclear Option/Blueprints/` instead of Desktop or `BepInEx/plugins/EditorPlus/Blueprints`.
- Removed in-editor **grid snap** placement mode (Atomic Builder grid tab / clamp patches).

### Fixed

- **Blueprint save 0 objects** — Global vs transform position mismatch no longer drops all units during filter; rejects empty saves.
- **Paste “no spawnable units”** — Empty blueprints from the save bug; fixed with capture logic above.
- **Newtonsoft.Json** — `Newtonsoft.Json.dll` copied beside plugin on build to prevent paste `TypeLoadException`.
- **Large paste console spam** — Reduced `TerrainHeightMap` noise via terrain clamp and throttled physics during spawn.

## [1.6.0] - 2026-05-24

### Added

- Initial Atomic Builder integration (save/paste UI, library, desktop-compatible JSON).

## [1.5.2] - 2026-04-05

### Fixed

- **Multiplayer / carrier deck:** Free-camera collision handling no longer runs outside the mission editor. It previously disabled physics on the **camera parent** (often the player aircraft), stripped `CharacterController` / collider behavior, and altered global physics queries near the camera—causing falls through carrier decks and server desync. Free-camera bypass now applies only when `MissionEditor` is active, only the camera object’s own components are toggled, and `CharacterController.Move` is no longer patched for the aircraft parent.

## [1.5.1] - 2026-03-23

### Fixed

- **Group copy/paste — formation scatter** — Paste uses a single anchor `GlobalPosition` plus relative offsets (translation-invariant).
- **Group copy/paste — rotation** — Copy stores `transform.rotation` instead of lagging `SavedUnit.rotation`.

### Added

- Multi-unit group copy/paste, formation paste at cursor, terrain-aware formation clamping.
