# Changelog

All notable changes to **EditorPlus** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

*Nothing yet.*

---

## [1.5.2] — 2026-04-05

### Fixed

- **Multiplayer / carrier deck:** Free-camera collision handling no longer runs outside the mission editor. It previously disabled physics on the **camera parent** (often the player aircraft), stripped `CharacterController` / collider behavior, and altered global physics queries near the camera—causing falls through carrier decks and server desync. Free-camera bypass now applies only when `MissionEditor` is active.

### Changed

- **Toolbar:** Graph grid button label shows **Graph Grid** on the button face (not hover-only).
- **Noclip:** Reduced log spam when terrain clamp is bypassed.

---

## [1.5.1] — 2026-03-23

### Fixed

- **Group copy/paste — formation scatter** — Paste converts the terrain/cursor anchor once to `GlobalPosition`, then places each unit with `anchorGlobal + relativeOffset`.
- **Group copy/paste — rotation** — Copy stores `transform.rotation` instead of lagging `SavedUnit.rotation`.

---

## [1.5.0] — earlier

### Fixed

- Copy-paste / duplicate **unique name** handling to avoid mission corruption from duplicate unit names.

### Added

- Multi-unit group copy/paste, formation paste at cursor, terrain-aware formation clamping, and related editor workflow improvements.
