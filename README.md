# beatmap-harvest

A Windows desktop app that collects every beatmap linked to an osu! profile and downloads them as `.osz` files.

You give it a profile. It reads that profile's score lists and every map it has ever played through the **official osu! API v2**, merges everything into one list of beatmapsets, and shows a summary. You choose what to download, and it fetches the `.osz` files from public mirrors. It skips maps you already have and resumes after interruptions.

> Beatmap Harvest is a community project. It is not affiliated with or endorsed by ppy Pty Ltd / osu!.

## Status

| Area | State |
|---|---|
| OAuth (client credentials), rate-limited API client | ✅ done |
| Harvest: best / firsts / pinned / recent scores + most played, merged per beatmapset | ✅ done |
| Summary (modes, stars, total length, most played) | ✅ done |
| Settings: credentials (encrypted), folders, parallel downloads | ✅ done |
| Detection of maps already owned (`done.json`, downloads folder, osu! stable `Songs`) | ✅ core done, UI pending |
| Selection model (filters per difficulty, include/exclude, saved configs) | ✅ core done, UI pending |
| Mirror downloads (`.part` → ZIP check → rename, mirror fallback, resume) | ✅ core done, UI pending |
| Selector and downloads screens | 🚧 next |

## Using it

### 1. Create your own osu! API credentials

Each user registers their own OAuth application. It is free, and the app never ships with anyone's secret.

1. Go to your osu! account settings: <https://osu.ppy.sh/home/account/edit#oauth>, under **OAuth**.
2. Click **New OAuth Application**. Use any name (e.g. `beatmap-harvest`) and leave the callback URL empty.
3. Click **Edit** on the new application and copy the **Client ID** and the **Client Secret** (behind **Show client secret**).
4. In Beatmap Harvest, open **Settings**, paste both values and click **Test & save**.

The credentials are stored encrypted with Windows DPAPI. Only your Windows account on that machine can read them, and the secret is only ever sent to `osu.ppy.sh`. Do not use the *Legacy API* key; this app uses API v2.

### 2. Harvest a profile

On **Harvest**, enter a profile URL (`https://osu.ppy.sh/users/123`), a numeric id or a username, then click **Start harvest**. The API allows about one request per second, so a large profile takes a minute or two.

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

## Architecture

```
BeatmapHarvest.slnx
├─ src/BeatmapHarvest.Core          net10.0 class library: all logic, no UI, no Windows APIs
│  ├─ Osu/          API v2 models, OAuth token provider, rate-limited client (IOsuDataSource), profile input parser
│  ├─ Harvesting/   ProfileHarvester (pagination), MapsetAggregator (merge), HarvestSummary
│  ├─ Library/      OwnedMapsetScanner, DownloadLedger (done.json): what is already on disk
│  ├─ Downloads/    BeatmapDownloader (mirrors, ZIP check, .part), DownloadRunner (parallel queue), SongsFolder
│  ├─ Selection/    SelectorConfig, SelectionEngine (filters / custom modes)
│  └─ Storage/      AppPaths, AtomicFile, settings and harvest stores
├─ src/BeatmapHarvest.App           WPF window hosting a BlazorWebView (WebView2)
│  ├─ Components/   Razor UI (layout, pages)
│  ├─ Services/     DI setup, DPAPI credential store, HarvestSession (state shared by pages)
│  └─ wwwroot/      HTML host page and CSS, embedded in the assembly
└─ tests/BeatmapHarvest.Core.Tests  xUnit tests for the Core
```

Design rules:

- **Core knows nothing about the UI.** The app only composes services and renders state. A CLI or another UI could reuse Core unchanged.
- **The data source is an interface** (`IOsuDataSource`). A proxy backend could replace direct API calls later without touching the harvesting logic.
- **Everything written to disk is atomic** (`.tmp` + flush + rename). Unreadable JSON is moved to `.corrupt` rather than lost.
- **Respect the API:** one request per second, the same request is retried on HTTP 429 (honouring `Retry-After`), and the token is refreshed once on 401.
- **Package versions are central** in `Directory.Packages.props`. Shared build settings live in `Directory.Build.props`, and warnings are treated as errors.

### Data flow

```
profile ─► osu! API v2 (scores + most played) ─► MapsetAggregator ─► beatmap_info.json + summary
        ─► selection (filters / manual) ─► owned detection ─► mirrors (.part → .osz) ─► done.json ─► Songs (optional)
```

### Where data is stored

Everything lives under `%LOCALAPPDATA%\BeatmapHarvest`:

| Path | Contents |
|---|---|
| `credentials.bin` | OAuth client id/secret, DPAPI-encrypted |
| `settings.json` | folders, parallel downloads, mirrors |
| `profiles/<user id>/beatmap_info.json` | last harvest of each profile |
| `done.json` | ids of beatmapsets whose download finished |
| `downloads/` | default download folder |

## Notes

- Official `.osz` downloads require a logged-in osu! session. This app downloads from public mirrors using only the beatmapset id and never asks for your osu! password or session cookie.
- Detection of owned maps reads osu! **stable**'s `Songs` folder. osu!**lazer** stores files by hash, so it cannot be detected this way.
- The `best` list returns at most 200 scores and `recent` only covers the last 24 hours. `most played` is what finds (almost) every map.

## Acknowledgements

The idea for this project came from [OSZsave](https://github.com/ByteWired9/OSZsave). Beatmap Harvest is a separate implementation written from scratch.

## Third-party notices

- `blazor.webview.js` from `Microsoft.AspNetCore.Components.WebView` is embedded in the executable (MIT, .NET Foundation).
- osu! is a trademark of ppy Pty Ltd. Beatmaps belong to their respective creators.

## License

[MIT](LICENSE) © 2026 Kriptonyte
