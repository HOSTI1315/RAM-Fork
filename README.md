# RAM Fork — Roblox Account Manager

[![Build](https://img.shields.io/github/actions/workflow/status/USER/REPO/build.yml?branch=main&label=build)](https://github.com/USER/REPO/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/USER/REPO?include_prereleases&sort=semver)](https://github.com/USER/REPO/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/USER/REPO/total)](https://github.com/USER/REPO/releases)
[![License](https://img.shields.io/github/license/USER/REPO)](LICENSE)

<p align="center">
  <a href="https://github.com/USER/REPO/releases/latest">
    <img src="https://img.shields.io/badge/⬇%20Download-Latest%20Release-2ea44f?style=for-the-badge" alt="Download Latest Release" />
  </a>
</p>

<p align="center">
  Manage many Roblox accounts at once. Launch them, watch them, and have them rejoin themselves if they crash.
</p>

---

## What is this?

A Windows desktop app that:

- Stores your Roblox account cookies safely on your PC (encrypted with a password only you know).
- Logs you into multiple accounts at the same time.
- Watches each running Roblox window and automatically rejoins if it disconnects or freezes.
- Organises hundreds of accounts into groups and lets you search, filter, and bulk-launch.

It's a from-scratch successor to the original [Roblox Account Manager (RAM) by ic3w0lf22](https://github.com/ic3w0lf22/Roblox-Account-Manager). It reads your existing RAM accounts directly — no manual export needed.

---

## System requirements

| Item | Required |
|---|---|
| **OS** | Windows 10 (1809+) or Windows 11, 64-bit |
| **Disk** | ~150 MB |
| **RAM** | 100 MB for the manager itself, plus whatever Roblox needs per account (typically 500 MB–1 GB each) |
| **Network** | Internet connection (for Roblox auth and presence checks) |
| **Other** | Roblox installed via the standard Roblox launcher (or Bloxstrap) |

Older Windows versions or 32-bit are not supported.

---

## What's new vs the original RAM

If you're already using the original RAM, here's what you get by switching:

- **Auto-rejoin.** When a Roblox window crashes, freezes, or disconnects, the fork relaunches it automatically. The original has no auto-rejoin.
- **Stronger encryption** for stored cookies. The fork uses a more modern key-derivation scheme; the original's encryption is still readable so your old data migrates cleanly.
- **Cookie-clobber fix** (also called the "773 fix"). Launching multiple accounts in quick succession used to occasionally swap the wrong cookie into the wrong window. The fork holds an exclusive lock during the launch window to prevent this.
- **Per-account proxy.** HTTP, SOCKS4, or SOCKS5, with authentication.
- **Visible status badges.** Each account row shows at a glance whether it's watching, in a grace period, rejoining, or failed.
- **Better keyboard navigation.** Ctrl+A, Shift-click range select, Home/End, search-as-you-type.
- **Numeric group sorting** — name a group `01 Farming` / `02 Trading` and they sort in that order. Makes 100+ accounts manageable.
- **Faster API calls** through background batching (presence checks for 100 accounts go out as one request, not 100).
- **No more `handle.exe`.** The original needed Microsoft Sysinternals' `handle.exe` to find Roblox's log files. The fork does it natively.

What is **not** there yet (vs the original RAM):

- Trade / economy parsing (intentionally dropped — out of scope).
- Some niche window-positioning options that lived in the original's right-click menu.
- Plugins (the architecture is ready for them, but no plugin system ships in this version).

---

## Migrating from the original RAM

The fork can read your existing RAM data directly. Your original install is **not modified** — copy your file across, and the original keeps working exactly as before.

### 1. Close the original RAM

Both apps reading the same file at the same time will corrupt it. Close it first.

### 2. Find your original `AccountData.json`

It's in:

```
%AppData%\RBX Alt Manager\AccountData.json
```

To open the folder: press <kbd>Win</kbd>+<kbd>R</kbd>, paste `%AppData%\RBX Alt Manager` and press Enter.

(If the folder doesn't exist there, you may have an older install that kept its data next to the .exe — check the folder where `RBX Alt Manager.exe` lives.)

### 3. Run the fork once

Launch RAM Fork. It creates its own folder at `%AppData%\RAM\` and then asks you to set a master password (more on this below). Close it after the password is set.

### 4. Copy your old file across

Copy `AccountData.json` from `%AppData%\RBX Alt Manager\` into `%AppData%\RAM\`. Don't rename it.

### 5. Launch the fork again

It detects the original file's format, decrypts your accounts, and shows them in the list. The list is read-only at this point — nothing is written back yet.

### 6. Save once to complete migration

Edit any account (even just bumping a tag) and save. The fork now re-encrypts the entire store under its own scheme. From now on, the file is in the new format.

### 7. Verify, then clean up

- Confirm every account is listed.
- Try launching one account to make sure it works.
- Once you're happy, you can uninstall the original RAM. Keep a copy of the original `AccountData.json` somewhere safe for a few weeks just in case.

A longer version with troubleshooting lives in [docs/MIGRATION.md](docs/MIGRATION.md).

---

## First launch — about the master password

> ⚠️ **The master password protects your Roblox cookies. If you forget it, your saved accounts cannot be recovered.**
>
> The password is used to derive the encryption key — it is **never stored** anywhere on your PC. There is no "forgot password" link, no recovery email, no support ticket. We genuinely cannot help you if you lose it.

Practical advice:

- **Use a password manager** (Bitwarden, 1Password, KeePass — anything that gets backed up). Type the password once, save it there.
- **Don't reuse a weak password.** The data this protects is high-value (Roblox cookies = full account access). Pick something long.
- **Test that you remember it** by reopening the app after first setup, before you've added many accounts.

If you do lose it, you can delete `%AppData%\RAM\` and start over — but you'll need to re-add every account from scratch (cookies and all).

---

## "Windows protected your PC" — SmartScreen warning

When you run the .exe for the first time, Windows may show a blue dialog:

> Windows protected your PC. Microsoft Defender SmartScreen prevented an unrecognised app from starting. Running this app might put your PC at risk.

This appears on **every** unsigned application until it builds up enough downloads for Microsoft to mark it as trusted, regardless of whether it's safe. It's not a virus warning — it just means the file isn't carrying a paid code-signing certificate yet.

To bypass:

1. Click **More info** (small link in the dialog).
2. A **Run anyway** button appears at the bottom.
3. Click it.

Windows remembers the choice — you won't see the warning again for that file.

If you'd rather not bypass it, you can build from source yourself (see the [For developers](#for-developers) section at the bottom).

---

## Antivirus says it's a virus — false positive

Some antivirus tools flag this app as suspicious. The most common reasons:

- The fork ships as a single-file executable that unpacks itself on first run. This pattern is also used by malware packers, so heuristic scanners sometimes flag it without doing real analysis.
- It bundles a native cryptography library (`libsodium`), which is unusual for everyday apps and trips signature databases that haven't seen it before.
- It's not signed with an Authenticode certificate, which removes one trust signal that AV tools weight heavily.

What to do:

1. **Check the file on [VirusTotal](https://www.virustotal.com/)** — paste the link or upload the .exe. If only a few obscure engines flag it (and the well-known ones like Microsoft Defender, Kaspersky, ESET don't), that's almost certainly a false positive.
2. **Compare the SHA-256** shown on the GitHub release page against the one your machine sees. If they match, your download wasn't tampered with.
3. **Submit a false-positive report** to your AV vendor (links: [Microsoft](https://www.microsoft.com/en-us/wdsi/filesubmission), [Kaspersky](https://opentip.kaspersky.com/), [ESET](https://www.eset.com/int/support/sample-submission/)). They usually fix the database within a few days.
4. **Last resort:** add an exclusion in your AV for the install folder. Only do this if you trust the source — never blindly exclude folders for software from random places.

If you're seeing a flag from a major AV (Microsoft Defender, Kaspersky, Bitdefender, ESET, Norton, McAfee), please open an issue on this repo with the AV name and the detection name. We'll submit a vendor report ourselves.

---

## Screenshots

See [docs/SCREENSHOTS.md](docs/SCREENSHOTS.md).

---

## Credit

This fork stands on the shoulders of three predecessor projects:

- [ic3w0lf22's Roblox Account Manager](https://github.com/ic3w0lf22/Roblox-Account-Manager) — the original. Source of the cookie/auth logic, the encrypted store, custom fields, recent games, window positioning.
- [Roblox Account Manager 4](https://github.com/Roblox-Account-Manager/Roblox-Account-Manager-4) — Tauri/Rust fork. Source of the cookie file lock, FPS-unlock profiles, window-title watcher, memory-threshold killer, numeric group sorting.
- [ReJoin](https://github.com/lkaht/ReJoin) — Python tool. Source of the auto-rejoin state machine and the private-server share-link parser.

Where this fork diverges from all three, source files contain a comment explaining why.

---

## License

To be added. (Will be added by the project owner before the first public release.)

---

<details>
<summary><strong>For developers</strong> — building from source, running tests, project layout</summary>

### Build from source

Requirements: **.NET 10 SDK** on Windows.

```powershell
git clone https://github.com/USER/REPO.git
cd REPO
dotnet build RobloxAltManager.slnx -c Release
dotnet run --project src/RAM.UI -c Release
```

### Run tests

```powershell
# Unit tests (xUnit)
dotnet test tests/RAM.Roblox.Tests/RAM.Roblox.Tests.csproj
dotnet test tests/RAM.Storage.Tests/RAM.Storage.Tests.csproj

# End-to-end smoke tests (console runner; non-zero exit on failure)
dotnet run --project tests/RAM.SmokeTests -c Debug
```

Or via the solution:

```powershell
dotnet test RobloxAltManager.slnx
dotnet run --project tests/RAM.SmokeTests -c Debug
```

Test counts: **111 unit (RAM.Roblox.Tests)** + **116 unit (RAM.Storage.Tests)** + **6 smoke scenarios**.

### Project layout

| Project | Purpose |
|---|---|
| `RAM.Core` | Domain models + abstractions. No UI/IO dependencies. |
| `RAM.Plugins.Abstractions` | Interface-only — extension seams. |
| `RAM.Storage` | Encrypted JSON store, INI settings, secret-redacting Serilog enricher. |
| `RAM.Roblox` | API client, auth, launcher, watchers, auto-rejoin FSM. |
| `RAM.App` | ViewModels (CommunityToolkit.Mvvm) + DI composition root. |
| `RAM.UI` | WPF Views, theme dictionary, custom controls. |

For a layered architecture diagram and per-project notes see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

### Single-file release build

```powershell
pwsh scripts/publish.ps1 -Version 1.0.0
```

Produces `release/RAM-Fork-v1.0.0-x64.exe` and a matching `.zip`. The `IncludeNativeLibrariesForSelfExtract` flag is **required** because `Sodium.Core` carries a native `libsodium.dll` that has to live inside the single-file extract — without the flag it would land next to the .exe and the single-file goal is defeated.

### Roadmap / open issues

See [ROADMAP.md](ROADMAP.md) for v1.x candidate work items, written as ready-to-paste GitHub issues.

</details>
