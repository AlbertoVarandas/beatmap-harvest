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
│  ├─ Osu/          API v2 models, OAuth token provider, rate-limited client (IOsuDataSource), profile input parser
│  ├─ Harvesting/   ProfileHarvester (pagination), MapsetAggregator (merge), HarvestSummary
│  ├─ Library/      OwnedMapsetScanner, DownloadLedger (done.json): what is already on disk
│  ├─ Downloads/    BeatmapDownloader (mirrors, ZIP check, .part), DownloadRunner (parallel queue), SongsFolder
│  ├─ Selection/    SelectorConfig, SelectionEngine (filters / custom modes), MapsetSort, SliderRange
│  ├─ Display/      StarColor (osu!'s difficulty colour gradient)
│  └─ Storage/      AppPaths, AtomicFile, settings and harvest stores
├─ src/BeatmapHarvest.App           WPF window hosting a BlazorWebView (WebView2)
│  ├─ Components/   Razor UI (layout, pages)
│  ├─ Services/     DI setup, DPAPI credential store, HarvestSession / DownloadSession / SelectorState (state shared by pages), file dialogs
│  └─ wwwroot/      HTML host page and CSS, embedded in the assembly
└─ tests/BeatmapHarvest.Core.Tests  xUnit tests for the Core
```

Design rules:

- **Core knows nothing about the UI.** The app only composes services and renders state. A CLI or another UI could reuse Core unchanged.
- **The data source is an interface** (`IOsuDataSource`). A proxy backend could replace direct API calls later without touching the harvesting logic.
- **Everything written to disk is atomic** (`.tmp` + flush + rename). Unreadable JSON is moved to `.corrupt` rather than lost.
- **Respect the API:** one request per second, the same request is retried on HTTP 429 (honouring `Retry-After`), and the token is refreshed once on 401.
- **Package versions are central** in `Directory.Packages.props`. Shared build settings live in `Directory.Build.props`, and warnings are treated as errors.
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
| `settings.json` | folders, parallel downloads, mirrors |
| `profiles/<user id>/beatmap_info.json` | last harvest of each profile |
| `done.json` | ids of beatmapsets whose download finished |
| `downloads/` | default download folder |

## Third-party notices

- `blazor.webview.js` from `Microsoft.AspNetCore.Components.WebView` is embedded in the executable (MIT, .NET Foundation).
