# Roadmap — v1.x candidates

This file captures concrete v1.x work items pulled out of `// TODO` markers and v1 design notes that were intentionally left as seams. Each entry is GitHub-issue-ready: copy the body into a new issue when the project moves to a permanent repo.

Items are independent unless noted. None are blocking for v1 release.

---

## 1. LaunchDialog should respect `initialTabIndex` from context-menu submenu

**Labels:** `ui`, `polish`, `good first issue`

**Context.** The row context menu in [`AccountListView.xaml`](src/RAM.UI/Views/AccountListView.xaml) has a "Launch" submenu with four entries (Place ID / Job ID / Follow user / Private server). All four currently invoke `OpenLaunchDialogCommand`, which opens the dialog on whichever tab the dialog last saved as default. The intent is that picking "Follow user…" lands you on the Follow-user tab, not Place ID.

**Where it goes wrong.** `OpenLaunchDialogCommand` accepts only the `AccountItemViewModel` parameter. There is no signal of which submenu entry triggered it.

**Suggested fix.**
1. Extend the command signature in [`AccountListViewModel.OpenLaunchDialog`](src/RAM.App/ViewModels/AccountListViewModel.cs) to take an optional `LaunchTabKind` enum (`Place`, `JobId`, `FollowUser`, `PrivateServer`).
2. Add a `LaunchDialogRequested` overload or a new `EventArgs` carrying the tab hint.
3. In `LaunchDialogViewModel`, set `SelectedTabIndex` from the hint on construction.
4. Update each submenu `MenuItem` in `AccountListView.xaml` to pass its corresponding `CommandParameter`. The current shared `CommandParameter="{Binding ...DataContext}"` becomes a `MultiBinding` carrying the item + tab hint, or split into two commands.

**Acceptance:** clicking "Follow user…" opens the dialog with the Follow-user tab active without requiring an extra click.

---

## 2. Tag-input mini-dialog (replaces hardcoded `null` parameter)

**Labels:** `ui`, `dialog`

**Context.** `AddTagToSelectedCommand` in [`AccountListViewModel`](src/RAM.App/ViewModels/AccountListViewModel.cs) accepts a `string? tag` parameter. The current implementation:

```csharp
[RelayCommand(CanExecute = nameof(HasSelection))]
public void AddTagToSelected(string? tag)
{
    if (string.IsNullOrWhiteSpace(tag)) return;
    // TODO Step 7.4: prompt for tag string via IDialogService when called with null.
    ...
}
```

The bulkbar button in `AccountListView.xaml` invokes the command without a parameter, so currently tag-add is a silent no-op.

**Suggested fix.**
1. Add a `PromptForTextAsync(string title, string label, string? initial = null)` method to [`IDialogService`](src/RAM.Plugins.Abstractions). Returns `string?` — null on cancel.
2. Implement in `WpfDialogService` using a small overlay dialog (same pattern as `ConfirmDialog`). Reuse `Dialogs/ConfirmDialog.xaml` as a template — single-line `TextBox`, OK/Cancel.
3. In `AccountListViewModel.AddTagToSelected`, when `tag is null`, call `_dialogs.PromptForTextAsync("Add tag", "Tag name")` and recurse with the result.
4. Validate: trim, reject empty, reject duplicates per-account.

**Acceptance:** clicking "Add tag" in the bulkbar opens a prompt; entering "trader" applies the tag to all selected accounts.

---

## 3. Move-to-group: support creating a new group

**Labels:** `ui`, `dialog`

**Context.** `MoveSelectedToGroupCommand` is wired through the row context menu's "Move to group ▶" submenu, populated dynamically from `Groups`. There is no entry to *create* a new group on the fly — users currently have to open the detail panel and type into the Group field manually.

**Suggested fix.**
1. Add a "New group…" entry as the first item in the `Move to group` submenu (use a `Separator` to divide it from the dynamic group list).
2. Bind it to a new `MoveSelectedToNewGroupCommand` that calls `IDialogService.PromptForTextAsync` (depends on issue #2 above), then forwards the result to `MoveSelectedToGroup(name)`.
3. Validate: trim, reject empty, allow numeric prefix (`01 New`).
4. Auto-select the newly created group in the sidebar after the move.

**Acceptance:** with N accounts selected, "Move to group → New group…" prompts, accepts "03 Trading", moves all N, sidebar refreshes, and the new group is the active filter.

**Depends on:** issue #2 (PromptForTextAsync)

---

## 4. Symmetric exit animations for dialog overlays

**Labels:** `ui`, `polish`

**Context.** Dialog overlays (Confirm, Add Account, Launch, Settings) currently fade in with a `Storyboard` triggered on `IsOpen=true` (see `App.xaml` styles). The dismiss path sets `IsOpen=false`, which removes the dialog instantly — no fade-out. The asymmetry is jarring on slower hardware where the open animation is visible.

**Suggested fix.**
1. Define a `FadeOutStoryboard` mirroring the existing fade-in (200ms, opacity 1→0, optional ScaleTransform 1.0→0.96).
2. Add a `Trigger` on `IsOpen=False` that runs the fade-out.
3. **Critical:** the dialog must remain in the visual tree for the duration of the animation. Use `Visibility="Visible"` while opacity animates, then collapse via the storyboard's `Completed` event, OR use a `BooleanToVisibilityConverter` with a delay binding. Skipping this step causes the animation to be cut off.
4. Test with the four existing dialogs — Confirm, AddAccount, Launch, Settings — to ensure none of them dispose VM state mid-animation.

**Acceptance:** clicking Cancel or pressing Esc on any dialog runs a visible fade-out (~200ms) before the dialog disappears.

---

## 5. `IObservable<RejoinWorkerState>` for multi-consumer subscriptions

**Labels:** `architecture`, `auto-rejoin`

**Context.** [`RejoinWorker`](src/RAM.Roblox/Rejoin/RejoinWorker.cs) currently emits state changes through a single `Action<RejoinWorkerState>?` callback set at construction. The doc comment on [`IRejoinManager.OnAccountLaunched`](src/RAM.Roblox/Rejoin/IRejoinManager.cs) acknowledges this:

> v1: single callback consumer (UI). Future versions may evolve to `IObservable<RejoinWorkerState>` for multi-consumer subscription (UI + logging + plugins).

Reasons to do it:

- Plugins via `IPluginHost` should be able to react to FSM state without going through the UI.
- A diagnostic logger that records full transition streams (vs the current per-line `LogDebug`).
- Webhook / external notification channels (Discord ping on `Error`).

**Suggested fix.**
1. Add a `public IObservable<RejoinWorkerState> StateChanges { get; }` property on `RejoinWorker`. Backing impl: a `Subject<RejoinWorkerState>` wrapped in `AsObservable()`. Pull `System.Reactive` (or a small homegrown `Subject`) — `System.Reactive` is the boring choice.
2. Keep the existing `Action<RejoinWorkerState>?` callback for binary back-compat with the UI wiring; route it through the same Subject so callback consumers and observable consumers see identical streams.
3. `RejoinManager.Snapshot()` already returns a point-in-time map; consider also exposing `IObservable<(ulong UserId, RejoinWorkerState State)>` aggregating all workers, so multi-account UIs / plugins don't have to subscribe per-worker.
4. Dispose semantics: the Subject must `OnCompleted` on `RejoinWorker.DisposeAsync` — verify with a test that subscribers receive completion before the manager removes the worker.

**Acceptance:** unit test demonstrates two subscribers (one synchronous, one async) both receive identical transition streams from a single worker; both receive `OnCompleted` on dispose.

**Estimated effort:** half a day (the Subject is simple, but the test matrix needs to cover dispose + late-subscriber semantics).

---

## 6. Auto-attach to surviving Roblox processes on app start

**Labels:** `auto-rejoin`, `feature`

**Context.** Currently the rejoin worker is spawned only via `IRejoinManager.OnAccountLaunched`, which is invoked from `AccountListViewModel.LaunchCustomAsync` after a successful launch. If the user closes RAM while accounts are running, then re-opens RAM, the surviving Roblox processes are unmonitored — a disconnect won't trigger a relaunch.

**Suggested fix.**
1. On `AccountListViewModel.LoadAsync` (after accounts are loaded), enumerate processes named `RobloxPlayerBeta` (or `RobloxPlayerBeta.exe`).
2. For each, attempt to recover the BrowserTrackerID from the process command line (it's in the URI passed to the launcher) — use WMI / `ManagementObjectSearcher` for `Win32_Process.CommandLine`.
3. Cross-reference each running process against the loaded `Account.BrowserTrackerId` (RAM persists this per-account). On match, invoke a new `IRejoinManager.OnAccountReattach(Account, pid, trackerId, target)` overload that fires `SessionStarted` without going through `ILauncher.LaunchAsync`.
4. Default `LaunchTarget` for re-attached sessions: `LaunchTarget.Place(0)`. Document that the re-attached worker can't trigger a meaningful relaunch (no JobId), but it *can* clean up zombie processes and surface watcher state to the UI. Auto-rejoin on disconnect for re-attached sessions falls back to last-known place from `RecentGames` if available.
5. Edge cases: same `BrowserTrackerId` matched to multiple PIDs (shouldn't happen but possible after a crash/restart) — pick the most-recently-started one.

**Acceptance:** start RAM with two Roblox processes already running (each launched from a previous RAM session), and within 10s the worker badge for both accounts shows "watching".

**Caveats.**
- WMI access can be slow (250–500ms first call). Run the scan in the background, don't block `LoadAsync`.
- Reading `Win32_Process.CommandLine` requires the process to be visible to the current user — admin-launched Roblox won't be detectable from a non-admin RAM. Document this.
- The privacy footprint of WMI scans is low (we only read our own processes), but worth noting in the privacy section of the README if one is ever added.

**Estimated effort:** 1–2 days (the WMI piece + edge cases).

---

## 7. Per-account embedded browser (WebView2)

**Labels:** `ui`, `feature`, `auth`

**Context.** The original RAM ships an embedded browser per account: a side-panel WebView2 instance with that account's `.ROBLOSECURITY` cookie pre-loaded, so users can interact with the Roblox web (settings, trades, chat, group admin) without copying cookies between Chrome profiles or going through the regular `roblox.com` login flow.

This is a significant UX feature for people managing many accounts — it's how the original RAM is positioned vs raw cookie launchers. **It is not implemented in v1 of this fork.** End-user testing of v1 surfaced this as a gap relative to the original.

## Suggested fix

1. Add `Microsoft.Web.WebView2.Wpf` NuGet to `RAM.UI`. Bundle the WebView2 runtime via the evergreen bootstrapper (most Win10/11 machines have it; the bootstrapper covers the rest with a small download on first launch).
2. Create a new view: `RAM.UI/Views/AccountBrowserView.xaml` with a single `WebView2` control filling the panel. Add a "Browser" tab to `AccountDetailView.xaml` alongside the existing fields tab, OR open the browser as a separate overlay/window.
3. Per-account cookie isolation: WebView2 supports `CoreWebView2CreationProperties.UserDataFolder` — give each account its own folder under `%AppData%\RAM\webview\<userId>\`. This keeps cookies, cache, IndexedDB strictly partitioned. Critical: users with 50 accounts open all browsers → 50 user-data folders. Investigate disk footprint and add cleanup on `OnAccountRemovedAsync`.
4. Cookie injection: on first navigation to `roblox.com`, inject the account's `.ROBLOSECURITY` via `CoreWebView2CookieManager.AddOrUpdateCookieAsync`. Mark the cookie HttpOnly + Secure + SameSite=None to match what Roblox's own login sets.
5. Login state observation: subscribe to `CookieAdded` events. If the user logs out from inside the browser, Roblox clears the cookie — we may want to detect that and warn the user (but NOT auto-update the stored cookie, since the new cookie may be invalid or for a different account).
6. Refresh-cookie flow: add a button "Use current browser cookie" that grabs the live cookie from the WebView2 and updates the account's stored cookie. This is the main use case for the embedded browser — re-auth without manual export.

## Acceptance

1. Open the detail panel for any account → click "Browser" tab → see a logged-in `roblox.com` page for that account.
2. Open the same account's browser in a different tab from a different account → the two sessions are isolated (no cross-account cookie leak).
3. Click "Use current browser cookie" → the stored `.ROBLOSECURITY` updates, future launches succeed with the new cookie.

## Caveats

- **Disk footprint.** WebView2 user-data folders can grow to 100+ MB each with cache. For 50 accounts that's 5 GB. Document this; consider an "evict cache" maintenance command.
- **Memory.** Each WebView2 process is ~50–100 MB. Lazy-create on first browser open, not on app startup.
- **WebView2 runtime.** If the runtime isn't installed, the WebView2 control silently fails to render. Detect this on startup and show a one-time install prompt (the bootstrapper download is < 2 MB).
- **Roblox anti-automation.** The Roblox web flags certain automated patterns. Manual interaction in WebView2 should be indistinguishable from Edge / Chromium, but if Roblox starts blocking embedded WebView2 traffic, this feature breaks. No mitigation other than detect + report.

## Estimated effort

3–5 days end to end (WebView2 wiring is small; cookie isolation + UX polish + the "use current cookie" flow are the work).

---

## How to convert these into GitHub issues

Once the project moves to its permanent repo:

```bash
gh issue create --title "LaunchDialog should respect initialTabIndex from context-menu submenu" \
                --body-file <(awk '/## 1\./,/^---$/' ROADMAP.md) \
                --label ui,polish
```

Or just paste each section into the GitHub issue editor — GitHub renders the H2 headings and labels exactly as written.
