# Archipelago Party Launcher — Development Plan

A Windows desktop app that takes a group from "who's playing what" to a running Archipelago
server: pick players → pick each player's game and preset → generate YAMLs → generate the
multiworld → host it → hand each player what they need.

Origin: design discussion in `Plan.pdf` (ChatGPT export). This document supersedes it where
they differ.

---

## 1. Scope

**In scope (through v0.3)**
- Detect the local Archipelago install and verify its version.
- Ship pinned, known-good `.apworld` files and install/repair them.
- Arbitrary number of players, each with a game + preset + small overrides.
- Build valid player YAMLs (no hand-editing).
- Run `ArchipelagoGenerate` and `ArchipelagoServer`.
- Save sessions so a past game can be inspected or re-hosted.
- Per-player handoff packages (patch file + the game's pinned `.apworld` + README).

**Out of scope**
- ROM/ISO distribution, downloading, or patching.
- Emulator installation.
- Router config / UPnP (port forwarding stays a one-time manual step).
- Auto-downloading APWorlds from the internet.
- Mac/Linux. *Not planned, but not blocked*: `Core` stays UI-agnostic so an Avalonia front end
  could be added later without rewriting logic.

---

## 2. Verified environment facts

Checked against the dev machine on 2026-10-24 — re-verify when Archipelago is upgraded.

| Fact | Value |
|---|---|
| .NET SDK | 10.0.201 |
| Archipelago install | `C:\ProgramData\Archipelago` (version in `manifest.json` → `version: [0,6,8]`) |
| Custom worlds dir | `C:\ProgramData\Archipelago\custom_worlds\` (`dk64.apworld`, `Twilight Princess.apworld`) |
| Option templates | `Players\Templates\<Game Name>.yaml` — one fully commented template per installed game |
| Generator flags | `--player_files_path <dir>`, `--outputpath <dir>`, `--seed`, `--spoiler`, `--race`, `--skip_output` |
| Server usage | `ArchipelagoServer.exe [--port N] [--password P] [--savefile F] <multidata .zip>` |
| Output | `output\AP_<seed>.zip` (+ `.apsave` once hosted) |

**Gotchas found**
- `ArchipelagoGenerate.exe` ends with `Press enter to close.` and blocks on stdin. The runner must
  redirect stdin and close it (or write a newline) or the process never exits.
- `ArchipelagoServer.exe` reads console commands from stdin (`/exit`, `/players`, …). Keep stdin
  open and use it for graceful shutdown instead of killing the process.
- The launcher never writes to Archipelago's `Players` folder or `host.yaml`. Its only write into the
  install is `repair` → `custom_worlds`, which moves replaced files to
  `custom_worlds\.apparty-backup\<yyyyMMdd-HHmmss>\` first. Verified 2026-10-25: the generator ignores
  that folder.
- Generation reads ROM paths from `host.yaml`. DK64's ROM is already configured; a fresh machine may
  get a file-picker prompt on first generation. Surface that, don't fight it.
- Players need the same `.apworld` as the host on their own machine: the game's client (launched
  from the Archipelago Launcher) ships inside the APWorld. A player missing it, or holding a
  different version, can't start or connect the client.
- The pinned TP APWorld has no manifest; the generator warns it "will stop working with
  Archipelago 0.7.0". Upgrading Archipelago means pinning a newer TP world (new `apWorldSha256`).

---

## 3. Solution layout

Projects sit flat next to the solution; `src` / `tests` are Visual Studio solution folders only.

```
ArchipelagoPartyLauncher/
├── ArchipelagoPartyLauncher.slnx
├── ApParty.Core/           net10.0 class library — ALL logic, no WPF references
├── ApParty.Cli/            net10.0 console — dev harness, scriptable, used before the UI exists
├── ApParty.App/            net10.0-windows WPF — thin MVVM layer over Core
├── ApParty.Core.Tests/     xUnit (TestSupport/ helpers are linked into Cli.Tests)
├── ApParty.Cli.Tests/      xUnit — drives CliApp.Run with in-memory writers
├── kit/                    shipped data (copied next to the .exe)
│   ├── kit.json            kit version + tested Archipelago version
│   └── games/
│       ├── twilight_princess/
│       │   ├── game.json
│       │   ├── Twilight Princess.apworld   (gitignored, copied in locally)
│       │   └── presets/  streamlined.yaml, …
│       └── dk64/
│           ├── game.json
│           ├── dk64.apworld                (gitignored, copied in locally)
│           └── presets/  standard.yaml, …
├── Plan.pdf                (gitignored)
└── PLAN.md
```

The repo is public, so `*.apworld` and `Plan.pdf` are gitignored, and fixtures and tests use
placeholder player names (Alice, Bob, Carol). A kit without its `.apworld` files still builds and
tests; `doctor` reports the missing files as kit warnings.

Machine-specific settings (Archipelago path, server port, last session) live in
`%APPDATA%\ApParty\settings.json`, never in the repo. Sessions live in
`%LOCALAPPDATA%\ApParty\sessions\`.

**Libraries**
- `YamlDotNet` — YAML read/write.
- `CommunityToolkit.Mvvm` — `ObservableObject`, `[RelayCommand]`, source-generated MVVM boilerplate.
- `Microsoft.Extensions.DependencyInjection` — wire services in `App.xaml.cs` / CLI `Program.cs`.
- `xUnit` for tests.

---

## 4. Core domain model (`ApParty.Core/Models`)

```csharp
record GameDefinition {           // deserialized from kit/games/<id>/game.json
    required string Id;           // "twilight_princess" — also the folder name
    required string DisplayName;
    required string ArchipelagoGameName;   // must match the YAML `game:` key exactly
    string? ApWorldFile;          // null for core games bundled with Archipelago
    string? ApWorldSha256;        // required when ApWorldFile is set
    required Version MinArchipelagoVersion;   // "0.6.5" in JSON
    string? PatchExtension;       // ".aptp", ".chunky", null
}

record Preset(string Id, string DisplayName, string GameId, string FilePath);

record PlayerSlot(string Name, string GameId, string PresetId) {
    IReadOnlyDictionary<string, object> Overrides;   // option → value, e.g. "early_shadow_crystal" → true
}

record Session(string Id, string Name, DateTime Created, string ArchipelagoVersion,
               IReadOnlyList<PlayerSlot> Players, string? MultidataPath, string? Seed);

record KitManifest(Version KitVersion, Version ArchipelagoVersion);   // ApParty.Core/Kit
```

Models are immutable; the WPF layer gets its own view models (M4). APWorld hashes live in each
`game.json` (`apWorldSha256`), next to the file they pin (M2). Per-game player instructions arrive
with handoff packages (v0.2).

Generator output naming (verified): `AP_<seed>_P<slot>_<Name><patchExtension>` inside `AP_<seed>.zip`.

---

## 5. Services (`ApParty.Core/<Feature>/`)

Each is an interface + implementation so tests can fake the filesystem/processes. Code is grouped
by feature folder (`Catalog/`, `Yaml/`, `Validation/`, `Archipelago/`, `ApWorlds/`, `Health/`,
`Kit/`), each its own namespace. `AddApPartyCore(kitDirectory)` registers everything as singletons.

| Service | Responsibility |
|---|---|
| `IArchipelagoLocator` | Check a list of search paths for an install; read version from `manifest.json`. Today the list is `--archipelago` or the default `C:\ProgramData\Archipelago`; settings file + "ask user" arrive with the UI (M4). |
| `IGameCatalog` | Load `kit/games/*/game.json` and their presets. |
| `IApWorldManager` | Verify `custom_worlds` against the kit by SHA-256; repair installs/replaces from the kit (backs up replaced files first). |
| `IEnvironmentChecker` | Combine locator + APWorld checks + version requirements into one report (`doctor`, UI status strip). |
| `IYamlBuilder` | Preset + `PlayerSlot` → YAML document. Pure function; most-tested class in the project. |
| `IPlayerValidator` | Name length/uniqueness, game key present, `game:` matches, `requires.version` ≤ installed. |
| `IProcessRunner` | Thin wrapper over `System.Diagnostics.Process` (stdin/stdout/stderr, exit code, cancellation). |
| `IMultiworldGenerator` | Write YAMLs to `session/players`, run generator with `--player_files_path`/`--outputpath`, parse result, collect per-player patch files. |
| `IServerHost` | Start/stop server for a session's multidata, stream log lines, report LAN address. |
| `ISessionStore` | Create/list/load sessions (`session.json` + folders). |
| `IHandoffPackager` | (v0.2) Build `PlayerPackages/<Name>/` with patch file + that game's pinned `.apworld` (copied from the kit) + README with install steps, zip each. |

### YAML rules for `IYamlBuilder`
- Load preset with YamlDotNet's representation model (`YamlStream`). Comments are dropped on
  save; the generator doesn't need them.
- Set top-level `name`.
- Override semantics: setting an option to a single value **replaces the whole weight mapping**
  with `{ value: 50 }`. (Changing only one key would leave other non-zero weights in place.)
  List options (`exclude_locations`) and dict options (`start_inventory`) are replaced wholesale.
- Never touch keys the preset doesn't mention unless overridden.
- Output filename: `<Name>_<GameId>.yaml`.

---

## 6. File formats

**`kit/kit.json`**
```json
{
  "kitVersion": "0.1.0",
  "archipelagoVersion": "0.6.8"
}
```
`archipelagoVersion` is the version the kit was tested with; `doctor` warns (doesn't fail) when the
installed version differs. Each game's `minArchipelagoVersion` is the hard floor. APWorlds are pinned
by SHA-256 in `game.json`: hashes, not version strings, prove two machines have identical worlds.
Show human-readable versions in the UI if the `.apworld` contains an `archipelago.json`.

**`kit/games/<id>/game.json`** — serialized `GameDefinition` (camelCase).

**Session folder**
```
sessions/2026-10-24_001_saturday-night/
├── session.json
├── players/      Alice_twilight_princess.yaml, Bob_dk64.yaml
├── output/       AP_<seed>.zip, extracted patch files
└── logs/         generate.log, server.log
```

---

## 7. Milestones

Each milestone has a concrete "done when." Don't start the next until it's met.

### M0 — Scaffolding — ✅ done 2026-10-24
- [x] Solution, five projects (Core, Cli, App, Core.Tests, Cli.Tests), references, packages.
- [x] Git repo (Git Bash), `.gitignore` incl. ROM/ISO extensions and `sessions/`.
- [x] Remove template placeholders (`Class1.cs`, `UnitTest1.cs`); DI wiring in App and Cli.
- **Done when:** `dotnet build` and `dotnet test` pass and the scaffold is committed.

### M1 — YAML builder (the core of the whole app) — ✅ done 2026-10-24
- `GameDefinition`, `Preset`, `PlayerSlot`; `IGameCatalog`; `IYamlBuilder`; `IPlayerValidator`.
- Create TP and DK64 game folders; derive the first preset of each from the real YAMLs used in the
  last seed (`Players\Alice.yaml`, `Players\Bob_DK64.yaml`), with `name: TEMPLATE`.
- **Golden test:** building `Alice` + TP + that preset produces a document semantically equal to the
  original seed YAML (compare parsed trees, not text). Same for DK64.
- CLI: `apparty yaml --name Alice --game twilight_princess --preset streamlined --out .`
- **Done when:** golden tests pass and the CLI writes a YAML that `ArchipelagoGenerate --skip_output`
  accepts.

### M2 — Environment — ✅ done 2026-10-25
- `IArchipelagoLocator`, `IApWorldManager`, `kit.json`.
- CLI: `apparty doctor` (prints an `[OK]`/`[WARN]`/`[FAIL]` checklist; exit 3 on problems),
  `apparty repair` (`[OK]`/`[FIXED]`/`[FAIL]`).
- **Done when:** `doctor` reports 0.6.8 + both worlds `[OK]`; deleting an apworld then `repair`
  restores it.

### M3 — Generation
- `IProcessRunner`, `IMultiworldGenerator`, `ISessionStore`.
- CLI: `apparty generate --session party.json` (list of players) → session folder with output zip.
- Move the YAML file naming (`<Name>_<GameId>.yaml`, invalid characters → `_`) from the CLI into
  Core, with tests for names that collide after sanitizing.
- The runner closes the generator's stdin so `Press enter to close.` can't hang it.
- **Done when:** a TP + DK64 multiworld generates end-to-end from the CLI with no manual steps,
  and failures surface the generator's error text.

### M4 — WPF UI → **v0.1 release**
- Main window: player list (add/remove/edit name, game dropdown, preset dropdown), environment
  status strip, Generate button, result view with "Open folder" per player.
- Package with `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`.
- **Done when:** a friend who has never seen the code can generate a seed from the zip.

### v0.2
- Server start/stop with live log + LAN address; saved sessions list with "Host again";
  handoff packages; per-game instructions text.

### v0.3
- Settings editor driven by `Players\Templates\<Game>.yaml`: parse option names, choices, ranges,
  and descriptions (descriptions are in comments — needs YamlDotNet's scanner with comments enabled,
  not the representation model). This is what eventually unlocks "any game Archipelago supports"
  without hand-written settings per game.
- PopTracker / client launch shortcuts.

---

## 8. Open questions

- Is the `.aptp` file needed by TP players at all, or only the server address? (Affects handoff README.)
  The `.apworld` is needed either way (§2).
- APWorld redistribution: settled for the repo (gitignored, see §3). Handoff packages hand `.apworld`
  files to the group, which is fine for friends; still check each world's license before shipping
  them inside a public **release** zip (M4).
- Player-side check: should players get a way to verify their own `custom_worlds` against the
  kit (e.g. the CLI's `doctor`/`repair`, or a hash in the README), instead of trusting a manual copy?
- Preset ownership: shared set for the group (current approach); revisit per-player favorites later.
- Should a newer Archipelago than the kit's tested version ever fail instead of warn (e.g. a
  per-game `maxArchipelagoVersion`)? Revisit when 0.7.0 ships.
