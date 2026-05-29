# EditorPlus 1.6.2 — Release notes

**Pre-release for [EditorPlus-Endar728](https://github.com/Endar728/EditorPlus-Endar728) users on 1.6.1.**

Adds duplicate and batch-rename workflow tools, plus toolbar and panel UI cleanup from community feedback.

---

## Install / upgrade

1. Copy from this release zip into `Nuclear Option\BepInEx\plugins\` (any subfolder is fine):
   - `com.nikkorap.EditorPlus.dll`
   - `Newtonsoft.Json.dll` **(required if not already installed)**
2. Replace the previous EditorPlus DLL if upgrading from 1.6.1.
3. Open the **mission editor** — new items are under the **Tools** toolbar button.

**Upgrade from 1.6.1:** Replace `com.nikkorap.EditorPlus.dll` only. All existing hotkeys (Ctrl+C / Ctrl+V / Ctrl+D / Ctrl+Alt+V) still work.

---

## What's new

### Duplicate in place (button + Ctrl+D)

- **Tools → Duplicate** or **Ctrl+D** duplicates the current selection with a small offset so copies do not stack on the originals.
- Works with box selection and multi-unit groups.

### Batch rename

- **Tools → Batch rename…** opens a panel in the **bottom-right** of the editor.
- Select multiple units (Shift+drag box select), enter a **name prefix** and **start number**, then **Rename selected**.
- Units are renamed in order (`Squad_A_1`, `Squad_A_2`, …) with uniqueness checks across the mission.

### Cleaner toolbar

- **Duplicate**, **Batch rename**, and **Graph grid** are grouped under one **Tools** dropdown.
- **Atomic Builder** toolbar label shortened to **Builder** to save space.
- Tools menu opens **below** the top bar instead of overlapping other buttons.

---

## Hotkeys (unchanged from 1.6.1)

| Shortcut | Action |
|----------|--------|
| **Ctrl+C** | Copy selected units |
| **Ctrl+V** | Paste copied units at cursor |
| **Ctrl+D** | Duplicate in place |
| **Ctrl+Alt+V** | Paste blueprint at cursor |
| **Delete** | Remove all selected units |

---

## Files in this release

- `com.nikkorap.EditorPlus.dll` — EditorPlus 1.6.2
- `Newtonsoft.Json.dll` — required dependency for blueprint paste

---

## Feedback

Report issues on the [GitHub repository](https://github.com/Endar728/EditorPlus-Endar728/issues). This is a **pre-release** — please test duplicate and batch rename on your missions before relying on them in production work.
