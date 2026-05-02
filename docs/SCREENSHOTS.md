# Screenshots

Visual reference for the README and the GitHub release pages. Replace each placeholder with a real PNG when the build is feature-stable.

Convention: store images in `docs/img/` (create the folder when adding the first one). PNG, 16:10 or 16:9, around 1280×800 for the main shots, 800×600 for dialogs. Compress with [tinypng.com](https://tinypng.com/) or `oxipng -o4` before committing.

---

## TODO

### 1. Main window — populated account list

`docs/img/main-window.png`

What to show:
- Sidebar with at least 2–3 groups, one selected, "All accounts" pill at top.
- 5+ account rows with mixed states: one Online (green dot), one InGame (blue), one Offline, one with the Re-auth banner (Status=Error).
- One row hovered so the inline action buttons (Launch / More) are visible.
- Search box with a placeholder query if you want to demo filtering.

### 2. Settings dialog — auto-rejoin section

`docs/img/settings-rejoin.png`

What to show:
- Settings overlay open over the main window (dimmed background visible).
- Auto-rejoin section visible with the helper text ("Changes apply on next account launch — running watchers keep their original snapshot…").
- All four numeric fields populated (check interval, grace period, memory threshold, window-title check).

### 3. Worker-state badge — the four states

`docs/img/badges.png`

What to show:
- Four account rows side-by-side or stacked, each in a different worker state:
  - **watching** (green check)
  - **grace** (amber clock)
  - **rejoining** (blue refresh icon)
  - **failed** (red alert)
- Crop tight so each badge is readable. This is a feature shot for the README's "What's new" section.

### 4. Add Account dialog — cookie tab

`docs/img/add-account.png`

What to show:
- Add Account overlay open.
- Cookie tab selected with a (fake/redacted) cookie pasted, showing the validation green check.
- Username / Display Name fields filled.

### 5. First launch — master password setup

`docs/img/first-launch-password.png`

What to show:
- The empty-state main window in the background.
- Master password setup dialog in the foreground.
- Both password fields visible (one filled, one being typed) — clearly mark this as a screenshot of the UI, not a real password.

### 6. Migration dialog — accounts loaded from original RAM file

`docs/img/migration-loaded.png`

What to show:
- Account list populated immediately after a successful migration from `%AppData%\RBX Alt Manager\AccountData.json`.
- Some way to indicate "this came from RAM" — a one-time toast / banner is ideal.

---

## Notes

- Don't include real cookies, real usernames, or real BrowserTrackerIDs in any screenshot. Redact or use throwaway test accounts.
- Use the dark theme as the primary look — it's the default and the one most users will see first.
- For the README, link directly to raw GitHub URLs (`https://github.com/HOSTI1315/RAM-Fork/raw/main/docs/img/...`) so the images render in the GitHub Releases page too.
