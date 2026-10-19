# Development

Notes for building and working on Beatmap Harvest. For using the app, see the [README](../README.md).

## Building

Requirements: Windows 10 1809+ / 11 (WebView2 runtime, included in current Windows) and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/BeatmapHarvest.App
```

### Single `.exe`

```bash
dotnet publish src/BeatmapHarvest.App -p:PublishProfile=win-x64
```

This produces `artifacts/publish/win-x64/BeatmapHarvest.exe`: one self-contained file of about 70 MB that needs no .NET install and no other files next to it.

## CI and releases

- **CI** (`.github/workflows/ci.yml`) builds and tests every pull request and every push to `main`.
- **Release** (`.github/workflows/release.yml`) runs when a version tag is pushed. It tests, publishes the single-file exe with the tag's version, and creates a GitHub Release with `BeatmapHarvest.exe` and its SHA-256 checksum. The release notes are generated from the merged pull requests.

To release, merge to `main`, then tag that commit:

```bash
git switch main
git pull
git tag v0.2.0
git push origin v0.2.0
```

A tag with a suffix (`v0.2.0-beta.1`) creates a pre-release, which the README's "latest release" link skips. The version in `Directory.Build.props` is only the default for local builds; releases take theirs from the tag.

The exe is not code-signed yet, so Windows SmartScreen warns on first run. The release workflow has a marked place for the signing step, before the checksum.

## Architecture

```
BeatmapHarvest.slnx
├─ src/BeatmapHarvest.Core          net10.0 class library: all logic, no UI, no Windows APIs
│  ├─ Osu/          API v2 models, app token (client credentials), rate-limited client (IOsuDataSource),
│  │                user sign-in (OsuUserAuth, LoopbackCallbackListener), profile input parser
│  ├─ Harvesting/   ProfileHarvester (pagination, progress estimate), MapsetAggregator (merge), HarvestSummary
│  ├─ Library/      osu! stable files: OsuInstall (+ cfg), OsuDatabaseReader (osu!.db), CollectionDatabase,
│  │                SkinCatalog; OwnershipIndex (in osu! / downloaded / none), DownloadLedger (done.json)
│  ├─ Downloads/    BeatmapDownloader (mirrors, ZIP check, .part), DownloadRunner (parallel queue), SongsFolder
│  ├─ Selection/    SelectorConfig, SelectionEngine (filters / custom modes / library), MapsetSort, SliderRange
│  ├─ Display/      StarColor (osu!'s difficulty colour gradient)
│  └─ Storage/      AppPaths, AtomicFile, settings and harvest stores
├─ src/BeatmapHarvest.App           WPF window hosting a BlazorWebView (WebView2)
│  ├─ Components/   Razor UI: layout (sidebar with profiles), pages, dialogs (map details, add to collection)
│  ├─ Services/     DI setup; app-wide state shared by pages (HarvestSession, ProfileLibrary, SelectorState,
│  │                DownloadSession, OsuLibrary, CollectionsService, OsuAccount); DPAPI files; OsuFilesHost
│  │                (https://osu.local/ → osu! folder, for skin images); file/folder dialogs
│  └─ wwwroot/      HTML host page and CSS, embedded in the assembly
└─ tests/BeatmapHarvest.Core.Tests  xUnit tests for the Core
```

Design rules:

- **Core knows nothing about the UI.** The app only composes services and renders state. A CLI or another UI could reuse Core unchanged.
- **The data source is an interface** (`IOsuDataSource`). A proxy backend could replace direct API calls later without touching the harvesting logic.
- **Everything written to disk is atomic** (`.tmp` + flush + rename). Unreadable JSON is moved to `.corrupt` rather than lost.
- **Respect the API:** one request per second, the same request is retried on HTTP 429 (honouring `Retry-After`), and the token is refreshed once on 401.
- **Package versions are central** in `Directory.Packages.props`. Shared build settings live in `Directory.Build.props`, and warnings are treated as errors.
- **osu!lazer's `client.realm` is only written by the experimental `LazerRealmWriter`, and only when the user turns it on.** Its schema changes several times a year (52 in 2026), and lazer moves aside a database it can't open and starts an empty one, so a bad write could wipe a user's library. Maps go to lazer through its command line (`osu!.exe a.osz b.osz …`, batches of 50, forwarded over IPC when lazer runs; `LazerImport`/`LazerImportService`), and collections, by default, through a classic `collection.db` that lazer's "Import from osu!stable" reads (`CollectionsService`, target `LazerViaStable` or `LazerFolder`). Installed lazer maps are read from the `.osu` files in its file store, not from Realm. Maps can be sent while lazer is open (that's what its IPC channel is for); saving collections for lazer is locked while it is open (`OsuProcessWatcher`, polled every 2 s and re-checked right before saving). Lazer's own collections, skins and set backgrounds are read by `LazerRealmReader` from a **copy** of `client.realm` (Realm 20.1.0, the version lazer uses, dynamic API with no model classes, Fody disabled). Dynamic opens use the file's own schema and never change its version (checked: a typed open at the old version still works afterwards); the stored version is learned from Realm's "less than last set version N" refusal on the copy. Skin images are served from lazer's store at `https://lazer.local/a/ab/<hash>`.
- **`LazerRealmWriter` (target `LazerDirect`)** refuses unless the file's Realm format (header byte, 24 for Realm 20.1.0) equals the one this app writes and `BeatmapCollection` has exactly the known shape (ID guid PK, Name, BeatmapMD5Hashes, LastModified, nothing else required). It writes into a copy, reads the copy back (same version, format and the expected hashes), checks the original's size and time didn't change meanwhile, backs the original up (last 5), and swaps the copy in with `File.Replace` from the same folder. It only adds collections or hashes, merging by name like lazer's own importer. Tested on synthetic databases and on a copy of a real one; a real lazer opening a file changed this way has not been tested, hence "experimental".
- **osu!'s files are read from copies and written only on request.** `osu!.db` is read into memory, so osu! can stay open. `collection.db` is written only by "Save to osu!", which refuses while osu! runs and keeps a timestamped backup.
- **Large lists are virtualized.** The Beatmaps list (`<Virtualize>`) keeps about 16 cards in the DOM. Measured with a synthetic profile of 8000 beatmapsets / 24000 difficulties: the page opens in ~0.1 s, filters and search apply in 20–35 ms, and scrolling stays smooth.
- **Text goes through the localizer.** UI text is written in English and wrapped in `L["…"]`, `L.F("…", args)` or `L.Html("…")`; `Services/Translations.cs` maps each English text to Portuguese (Portugal). A missing entry shows the English text. Format numbers and dates with `L.Culture` / `Localizer.ActiveCulture`, not `CultureInfo.CurrentCulture` (it flows with async calls and misses some renders). Core progress messages are templates (`HarvestProgress.Template` + `Args`) so the UI can translate them.
- **The UI is embedded.** `wwwroot` and Blazor's `blazor.webview.js` are embedded resources served by `EmbeddedBlazorWebView`, so the single-file `.exe` works on its own. New static files must go under `wwwroot`.

### Data flow

```
profile ─► osu! API v2 (scores + most played) ─► MapsetAggregator ─► beatmap_info.json + summary
        ─► selection (filters / manual) ─► owned detection ─► mirrors (.part → .osz) ─► done.json ─► Songs (optional)
```

### Data files

Under `%LOCALAPPDATA%\BeatmapHarvest`:

| Path | Contents |
|---|---|
| `credentials.bin` | OAuth client id/secret, DPAPI-encrypted |
| `account.bin` | signed-in user's tokens (optional), DPAPI-encrypted |
| `WebView2/` | WebView2 cache |
| `settings.json` | folders, parallel downloads, mirrors |
| `profiles/<user id>/beatmap_info.json` | last harvest of each profile |
| `done.json` | ids of beatmapsets whose download finished |
| `downloads/` | default download folder |

## Third-party notices

- `blazor.webview.js` from `Microsoft.AspNetCore.Components.WebView` is embedded in the executable (MIT, .NET Foundation).
