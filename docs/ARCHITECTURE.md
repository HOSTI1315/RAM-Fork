# Architecture

A short tour of the solution layout, the dependency direction between projects, and the extension seams reserved for v1.x work.

---

## Solution layout

```
RobloxAltManager.slnx
├── src/
│   ├── RAM.Core/                     domain models + abstractions
│   ├── RAM.Plugins.Abstractions/     extension-seam interfaces
│   ├── RAM.Storage/                  encrypted store + crypto + INI
│   ├── RAM.Roblox/                   API + auth + launcher + watchers + rejoin
│   ├── RAM.App/                      ViewModels + DI composition root
│   └── RAM.UI/                       WPF Views + theme + custom controls
└── tests/
    ├── RAM.Roblox.Tests/             111 unit tests
    ├── RAM.Storage.Tests/            116 unit tests
    └── RAM.SmokeTests/               6 end-to-end scenarios
```

## Layered model

Projects are arranged in four conceptual layers; references flow strictly inward / downward.

```
┌─────────────────────────────────────────────────────────────┐
│  UI               RAM.UI                                    │
│                   - WPF Views, custom controls, theme       │
│                   - Code-behind only for platform mechanics │
│                     (drag-drop, dispatcher hooks)           │
├─────────────────────────────────────────────────────────────┤
│  Application      RAM.App                                   │
│                   - ViewModels (CommunityToolkit.Mvvm)      │
│                   - DI composition root                     │
│                   - No WPF references — UI-toolkit-agnostic │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure   RAM.Roblox        RAM.Storage             │
│                   - HTTP / IO       - File IO / crypto      │
│                   - Process control - JSON / INI            │
│                   - Watchers / FSM  - Logging enrichers     │
├─────────────────────────────────────────────────────────────┤
│  Domain           RAM.Core          RAM.Plugins.Abstractions│
│                   - POCOs / records - Extension seams       │
│                   - Interfaces      - No implementations    │
│                   - No IO / no UI                           │
└─────────────────────────────────────────────────────────────┘
```

**Allowed references:**

- `RAM.UI` → `RAM.App` → `RAM.Roblox` + `RAM.Storage` → `RAM.Core` + `RAM.Plugins.Abstractions`
- `RAM.Roblox` and `RAM.Storage` may both reference `RAM.Core` and `RAM.Plugins.Abstractions` but **not each other**. Anything they need to share goes through `RAM.Core`.
- Test projects reference whichever production projects they exercise plus `xUnit`.

**Forbidden:**

- No project may reference `RAM.UI`. WPF stays leaf-only.
- `RAM.Core` references nothing.
- `RAM.Plugins.Abstractions` references nothing (so plugin authors can ship an assembly that depends only on this contract).

---

## Per-project notes

### `RAM.Core` — domain
POCOs and abstractions, zero IO. Records like `Account`, `AppSettings`, `UserPresence`, `RecentGame`, plus interface contracts: `IAccountStore`, `ILauncher`, `IPresenceProvider`, `IRobloxApi`. The `RejoinWorkerState` enum lives here so ViewModels can bind to it without referencing `RAM.Roblox`.

### `RAM.Plugins.Abstractions` — extension contracts
Interface-only assembly. Holds the seven extension seams that are registered in DI as no-op singletons in v1 and can be swapped for real implementations later: `IPluginHost`, `IPresenceProvider` (note: also surfaced via `RAM.Core`), `IRemoteControl`, `IThemeSource`, `ILocalizer`, `IDialogService`, `IFileDialogService`, `IAutoStartService`. Plugin authors ship assemblies that depend only on this project.

### `RAM.Storage` — persistence + crypto
- `Crypto/` — Argon2id KDF + XSalsa20-Poly1305 SecretBox via `Sodium.Core`. Legacy DPAPI and Argon2i readers for migration.
- `Json/AccountStore` — versioned envelope `{schema_version, kdf_variant, payload}`, 8-hour auto-backup with retention policy.
- `Ini/SettingsStore` — `RAMSettings.ini` reader/writer with `[meta] schema_version=N` migrator chain.
- `Logging/SecretRedactingEnricher` — Serilog enricher that scrubs cookies / CSRF tokens / auth tickets / bearer tokens before sink write.

### `RAM.Roblox` — Roblox-specific behavior
- `Api/` — `RobloxApi` (HttpClient + Polly retry / circuit / rate-limit), CSRF token cache, presence + thumbnail batchers (100 IDs / 50ms window).
- `Auth/` — cookie login, password + 2FA (email / authenticator), AuthTicket provider, PIN unlock client.
- `Launch/` — `roblox-player://` URI builder, `SingletonMutexBypass` (multi-instance), `CookieFileLock` (773 fix), `RobloxLauncher` orchestration.
- `ClientSettings/` — `ClientAppSettings.json` patcher with profiles (Normal / BottingPlayer / BottingBot).
- `Watchers/` — `FlogWatcher` (tails `player.log`), `WindowTitleWatcher`, `MemoryThresholdKiller`.
- `Rejoin/` — 5-state FSM (`RejoinWorker`) on a single-consumer `Channel<RejoinEvent>`; `RejoinManager` singleton owns `ConcurrentDictionary<ulong, RejoinWorker>`.

### `RAM.App` — view models + DI
- `ViewModels/` — `ShellViewModel`, `AccountListViewModel`, `AccountDetailViewModel`, `SettingsViewModel`, dialog VMs. Built on `CommunityToolkit.Mvvm` source generators.
- `Composition.cs` + `AppServiceCollectionExtensions.cs` — DI registration.
- `NoOps/` — default no-op implementations of every extension seam, registered at startup. Keeps the DI graph valid before any plugin loads.

### `RAM.UI` — WPF shell
- `Views/` — XAML for every screen + dialog overlay.
- `Controls/` — custom controls (e.g. `WorkerStateBadge`, `AvatarWithStatus`, `StatusDot`).
- `Themes/` — token dictionaries (`Tokens.Dark.xaml`, `Tokens.Light.xaml`), icon paths.
- `Services/` — WPF-specific implementations of `IDialogService`, `IFileDialogService`, registered over the no-op defaults.

---

## Extension seams

The seven interfaces in `RAM.Plugins.Abstractions` are the supported way to extend the app without touching its source:

| Seam | v1 implementation | Future feature it unlocks |
|---|---|---|
| `IPluginHost` | `NoOpPluginHost` (returns 0 plugins) | Nexus WebSocket, scripting sandbox, webhooks |
| `IPresenceProvider` | `PollingPresenceProvider` (real, in `RAM.Roblox.Api`) | WebSocket-based presence (replaces polling) |
| `IRemoteControl` | `NoOpRemoteControl` (`IsRunning=false`) | REST web server for headless control, botting orchestration |
| `IThemeSource` | `NoOpThemeSource` (single hardcoded theme) | Theme system + Catppuccin presets |
| `ILocalizer` | `NoOpLocalizer` (English passthrough) | i18n |
| `IDialogService` | `WpfDialogService` (in `RAM.UI`); fallback `NoOpDialogService` | — already real in v1 |
| `IFileDialogService` | `WpfFileDialogService`; fallback `NoOpFileDialogService` | — already real in v1 |
| `IAutoStartService` | `NoOpAutoStartService` | Autostart on PC startup (registry write) |

Plus a non-interface seam: `Settings.SchemaVersion` migrator chain in `RAM.Storage` — handles forward-compat of `.ini` and `.json` files when fields are added or moved.

---

## Auto-rejoin FSM, in one paragraph

`RejoinWorker` owns a `Channel<RejoinEvent>`. Five states: `Idle / Watching / GracePeriod / Rejoining / Error`. Producers (FLog watcher, window-title watcher, periodic memory + presence checks, the launcher continuation) only enqueue events. A single consumer task drains the channel and applies state transitions serially — no locks needed for state mutation. The grace timer is its own `CancellationTokenSource` so recovery (FLog Connected or presence InGame) cancels it before it fires. FLog wins over presence: a presence-Offline event is suppressed if the last FLog Connected marker arrived within `2 × GracePeriod`. See [`RejoinWorker.cs`](../src/RAM.Roblox/Rejoin/RejoinWorker.cs) for the dispatch table and [`RejoinWorkerTests.cs`](../tests/RAM.Roblox.Tests/Rejoin/RejoinWorkerTests.cs) for the per-transition + edge-case + concurrency-stress test suite.
