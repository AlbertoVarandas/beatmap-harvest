# Beatmap Harvest

Download every osu! beatmap you have ever played, in a few clicks.

Beatmap Harvest looks up an osu! profile and lists every map it has played or set a score on. You choose which ones you want, and it downloads them as `.osz` files ready for osu!. Maps you already have are skipped.

> Beatmap Harvest is a community project. It is not affiliated with or endorsed by ppy Pty Ltd / osu!.

## Requirements

- Windows 10 (version 1809 or newer) or Windows 11
- An osu! account (free)

Nothing else needs to be installed.

## Download

Download **`BeatmapHarvest.exe`** from the [latest release](https://github.com/AlbertoVarandas/beatmap-harvest/releases/latest) and run it. There is no installer; you can keep the file wherever you like.

Windows may show a "Windows protected your PC" warning the first time, because the app is not signed. Click **More info** → **Run anyway**.

## First-time setup: connect to osu!

Beatmap Harvest reads profiles through the official osu! API. To use the API, you create your own free "OAuth application" on the osu! website. This takes about a minute and only has to be done once.

1. Open your [osu! account settings](https://osu.ppy.sh/home/account/edit#oauth) and scroll to **OAuth**.
2. Click **New OAuth Application**.
3. Type any name (for example `beatmap-harvest`), leave **Application Callback URLs** empty and click **Register application**.
4. Click **Edit** on the new application. Copy the **Client ID**, then click **Show client secret** and copy the **Client Secret**.
5. In Beatmap Harvest, open **Settings**, paste both values and click **Test & save**.

Use the **OAuth** section, not *Legacy API*.

### Optional: sign in with your osu! account

Signing in adds a **My profile** button and your **friend list** (with one-click harvest) to the Harvest page. You approve Beatmap Harvest on the osu! website; the app never sees your password.

1. On the osu! website, click **Edit** on your OAuth application and add `http://localhost:7270/callback` to **Application Callback URLs** (Settings has a **Copy** button for it).
2. In **Settings → osu! account**, click **Sign in with osu!** and approve the app in the browser.

**Sign out** removes the sign-in from the app and cancels it on osu!. Downloads work the same either way: osu! does not let other apps download beatmaps, so they always come from mirrors.

Your client secret works like a password: don't share it. Beatmap Harvest stores it encrypted, readable only by your Windows account, and only ever sends it to osu.ppy.sh. It never asks for your osu! password.

## How to use

### 1. Harvest a profile

On **Harvest**, enter a profile link (`https://osu.ppy.sh/users/123`), a user id or a username, then click **Start harvest**.

A progress bar shows how far along it is. A large profile takes a minute or two, because osu! limits how fast the app can ask for data. At the end you see a summary: how many maps were found, your plays, game modes, star range and your most played maps.

You can harvest as many profiles as you like, yours or other players'. Each one appears under **Beatmaps** in the sidebar; click a name to see that player's maps. Each profile keeps its own selection, and **Remove profile…** forgets one (maps you downloaded stay where they are).

To look up a username made only of digits, put `@` in front of it (`@12345`); otherwise it is read as a user id.

### 2. Choose what to download

**Beatmaps** shows every map that was found. There are two ways to choose:

- **By filters**: everything that matches the filters is selected. You can filter by game mode, stars, length, how many times you played a difficulty, map status and where the map was found (best scores, first places, most played…). Click a map to add or remove it on top of the filters.
- **Hand-picked**: only the maps you click are selected.

Some useful extras:

- Click a cover to hear a preview of the song.
- Click **i** on a map (or a song in **Most played** on the Harvest page) to see its details: cover, BPM, length, ranked date, global plays, and every difficulty with its stars, CS, AR, OD, HP, max combo and your plays.
- Maps already in osu! are tagged **In osu!**, and maps you downloaded but haven't added to osu! yet are tagged **Downloaded**. Both are skipped unless you untick **Skip maps I already have**. The **Library** filter shows only maps that are in osu!, only downloaded, or not on this PC.
- Use the search box to find a song, artist, mapper or difficulty.
- **Outside the filters: Dim / Hide** chooses whether maps and difficulties that don't match the filters are shown faded or left out of the list.
- **Save selection…** and **Load selection…** keep your filters and choices in a file for later.

### 3. Download

Click **Download N maps**. The **Downloads** page shows the progress of each map. You can switch pages while it runs.

If you stop the download or close the app, start it again later: maps that were already downloaded are skipped, so it continues where it stopped.

### 4. Add the maps to osu!

On **Downloads**, click **Move downloads…**. By default this moves the `.osz` files into osu!'s Songs folder; the next time osu! starts, it imports them.

You can also open the downloads folder (**Open folder**) and double-click the `.osz` files, or drag them onto the osu! window.

## Your osu! library

**osu! library** shows the osu! (stable) installation Beatmap Harvest found, every beatmapset in it, and, for each harvested profile, how many of its maps are already in osu!, only downloaded, or missing. **Show missing** opens that profile's beatmaps with only the maps you don't have.

Beatmap Harvest reads osu!'s own map list (`osu!.db`) and the Songs folder, so it also recognises maps whose folder names don't start with their id. It only reads them; osu! can stay open. osu!lazer is not supported.

## Collections

**Collections** shows your osu! collections and lets you create, rename and delete them and remove maps from them. On **Beatmaps**, **Add to collection…** adds every difficulty of the selected maps to a collection. Maps not in osu! yet can be included too: Beatmap Harvest looks them up on osu! (about one per second), and they appear in the collection as soon as you download them.

Changes stay in Beatmap Harvest until you click **Save to osu!**. Close osu! first, because osu! rewrites its collections when it exits. Each save keeps a copy of the previous file next to it (`collection.db.<date>.bak`).

## Skins

**Skins** lists the skins installed in osu! with their author and a preview of their main elements (cursor, hit circles, rank letters, mania notes…), and marks the one in use.

## Settings

Each folder can be typed, picked with **Browse…**, or set back to its default with **Default**.

- **Downloads folder**: where `.osz` files are saved.
- **osu! folder**: your osu! (stable) installation. It is detected automatically; set it only if osu! is somewhere unusual.
- **osu! Songs folder**: used to find maps you already have. By default, the Songs folder osu! itself uses.
- **Move downloads to**: where **Move downloads…** puts the files. The default is the osu! Songs folder.
- **Parallel downloads**: how many maps download at the same time.

## FAQ

**Why do I need to create an OAuth application?**
It is how osu! lets third-party apps read public profile data. Each person uses their own, so no secret is shared inside the app.

**Where are the maps downloaded from?**
From public beatmap mirrors. Official osu! downloads require logging in to the website, and Beatmap Harvest never asks for your osu! password.

**Some maps failed to download.**
Mirrors don't always have every map, especially old or unranked ones. Try again later. Maps that already finished are not downloaded twice.

**I use osu!lazer. Does it detect the maps I already have?**
Not yet. Detection reads osu! stable's `Songs` folder, and lazer stores its files differently. You can still download maps and open the `.osz` files in lazer.

**Why does it find fewer "best" scores than I have?**
osu! only exposes your top 200 scores and the last 24 hours of recent plays. Most maps are found through your **most played** list, which includes every map you have played.

**Where does the app keep its data?**
In `%LOCALAPPDATA%\BeatmapHarvest`. **Settings → Open data folder** opens it. Deleting that folder resets the app.

## Problems or ideas

Open an [issue on GitHub](https://github.com/AlbertoVarandas/beatmap-harvest/issues).

## Acknowledgements

The idea for this project came from [OSZsave](https://github.com/ByteWired9/OSZsave).

osu! is a trademark of ppy Pty Ltd. Beatmaps belong to their respective creators.

## License

[MIT](LICENSE) © 2026 Kriptonyte
