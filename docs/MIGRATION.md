# Migrating from the original RAM

This document covers moving an existing account database from [ic3w0lf22's Roblox Account Manager](https://github.com/ic3w0lf22/Roblox-Account-Manager) (referred to below as "original RAM") into this fork.

The fork reads the original's `AccountData.json` directly. On first save it re-encrypts under Argon2id. **The original file is not modified during read** — your old install remains usable until you remove it yourself.

---

## Before you start

- Make sure the original RAM is **closed**. Both apps reading the file at the same time can corrupt it.
- Optional but recommended: **back up `AccountData.json`** to a separate folder. The fork makes its own backup on save, but a manual copy gives you a known-good fallback if anything goes wrong.

---

## Step 1 — locate `AccountData.json`

The original RAM stores `AccountData.json` in your roaming AppData folder:

```
%AppData%\RBX Alt Manager\AccountData.json
```

To open the folder: press <kbd>Win</kbd>+<kbd>R</kbd>, paste `%AppData%\RBX Alt Manager` and press Enter.

If the folder isn't there, you may have a very old build that kept its data next to the .exe — check the directory containing `RBX Alt Manager.exe`. If `AccountData.json` is in neither place, search Explorer-wide for the filename.

You may also see sibling files: `Settings.ini`, `RecentGames.json`, `AccountData.json.bak`. The fork only needs `AccountData.json` itself; the others are not read.

## Step 2 — find the fork's data directory

The fork stores its data under `%AppData%\RAM\`. To open it:

1. Press <kbd>Win</kbd>+<kbd>R</kbd>, type `%AppData%\RAM` and press Enter.
2. If the folder does not exist yet, launch the fork once. It creates the folder on first start, then you can close it.

## Step 3 — copy the file

1. Copy `AccountData.json` from the original RAM folder.
2. Paste it into `%AppData%\RAM\`.
3. Do **not** rename it.

## Step 4 — launch the fork

On first launch with an existing `AccountData.json`:

- The fork detects the original's encryption envelope (DPAPI, or Argon2i + libsodium SecretBox).
- It decrypts each account using the legacy reader.
- It loads the accounts into the UI. **Status:** read-only at this point — no file is written yet.
- On the **next save** (any account edit, or app exit), it re-encrypts the entire store under Argon2id + XSalsa20-Poly1305. The envelope's `kdf_variant` field flips from `argon2i` to `argon2id`.

You will be prompted for a password the first time the fork writes a new envelope. Pick one — the fork derives a fresh KDF salt and encrypts under it.

If the fork cannot decrypt the file (wrong password, corrupted envelope), it shows an error and refuses to write — your original file stays untouched.

## Step 5 — verify

Before deleting the original install:

- Confirm every account is present in the fork's account list.
- Try launching one account that you know works in the original — it should produce a working Roblox window in the fork too.
- Check `%AppData%\RAM\backups\` — after the first save, you should see a backup snapshot. The 8-hour auto-backup runs from this point on.

## Step 6 — clean up

Once you have confirmed the migration:

- The original RAM install can be uninstalled / its folder deleted.
- The original `AccountData.json` can be deleted (keep your manual backup somewhere safe for a few weeks just in case).

---

## What is NOT migrated

The following data does not transfer automatically — re-add it manually if you used these features in the original:

| Item | Why |
|---|---|
| `Settings.ini` from the original | Schema is incompatible. The fork uses its own `RAMSettings.ini` with versioned fields. |
| Per-account window positions stored outside `AccountData.json` | The original stored these inline in `AccountData.json`; if so they migrate. If you used the registry-based override, re-set per account. |
| `RecentGames.json` | Format diverged; the fork rebuilds recent games from launches. |
| Custom field names / column ordering preferences | UI layout config; fork has its own preferences. Custom field *values* on accounts do migrate. |

---

## Troubleshooting

**"File version not recognized"** — the fork's reader chain handles original RAM v1 (DPAPI) and v2 (Argon2i + SecretBox). If you see this on a current RAM file, please open an issue with the file's first 32 bytes (avoid sharing the whole file — it contains your cookies).

**Decryption fails with the right password** — make sure you copied the entire file, including the trailing newline if any. Compare file sizes between source and destination.

**Some accounts are missing after migration** — check the fork's logs (`%AppData%\RAM\logs\`). Per-account decrypt failures are logged with the account UserId; the rest still load.

**The original RAM still works after migration** — it should. The fork doesn't touch the source file. If you want to keep both running, just be sure not to launch them simultaneously against the same `AccountData.json` (the fork's copy lives in `%AppData%\RAM\`, so this is a non-issue once migration is complete).
