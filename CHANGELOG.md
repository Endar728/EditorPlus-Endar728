# Changelog

All notable changes to **EditorPlus** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

*Nothing yet.*

---

## [1.5.1] — 2026-03-23

### Fixed

- **Group copy/paste — formation scatter** — Pasting large selections (dozens of buildings/units) no longer throws the whole group across the map. The paste path used `(cursor + relativeOffset).ToGlobalPosition()` for every unit; the game’s `GlobalPosition` system uses a floating global origin and that conversion is **not** translation-invariant, so each unit ended up with an inconsistent world pose. Paste now converts the **terrain/cursor anchor once** to `GlobalPosition`, then places each unit with `anchorGlobal + relativeOffset` (same idea as vanilla hangar spawn).

- **Group copy/paste — rotation** — Pasted groups keep the correct facing. Copy stores each unit’s **`transform.rotation`** in world space instead of `SavedUnit.rotation`, which could lag behind gizmo / GroupFollowers edits until the mission was saved.

---

## [1.5.0] — earlier

### Fixed

- Copy-paste / duplicate **unique name** handling to avoid mission corruption from duplicate unit names.

### Added

- Multi-unit group copy/paste, formation paste at cursor, terrain-aware formation clamping, and related editor workflow improvements.

---

*When you ship a build, add a new `## [x.y.z]` section above `[Unreleased]` and reset Unreleased to empty or “Nothing yet.”*
