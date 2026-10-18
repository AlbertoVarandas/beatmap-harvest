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
3. Type any name (for example `beatmap-harvest`), put `http://localhost:7270/callback` in **Application Callback URLs** (Settings has a **Copy** button for it) and click **Register application**.
4. Click **Edit** on the new application. Copy the **Client ID**, then click **Show client secret** and copy the **Client Secret**.
5. Beatmap Harvest opens on **Settings**: paste both values and click **Test & save**. The other tabs unlock.

Use the **OAuth** section, not *Legacy API*.

### Optional: sign in with your osu! account

Signing in harvests your own profile straight away, keeps it at the top of the profile list (in bold), adds a **My profile** button, and unlocks **My friends**. You approve Beatmap Harvest on the osu! website; the app never sees your password.

1. Make sure `http://localhost:7270/callback` is in your OAuth application's **Application Callback URLs** (step 3 above; you can add it later with **Edit**).
2. In **Settings → osu! account**, click **Sign in with osu!** and approve the app in the browser.

**Sign out** removes the sign-in from the app and cancels it on osu!. Downloads work the same either way: osu! does not let other apps download beatmaps, so they always come from mirrors.

Your client secret works like a password: don't share it. Beatmap Harvest stores it encrypted, readable only by your Windows account, and only ever sends it to osu.ppy.sh. It never asks for your osu! password.

## How to use

### 1. Harvest a profile

At the top of **Beatmaps**, enter a profile link (`https://osu.ppy.sh/users/123`), a user id or a username, then click **Harvest**.

A progress bar shows how far along it is. A large profile takes a minute or two, because osu! limits how fast the app can ask for data. When it finishes, the profile opens on **Overview**: how many maps were found, plays, game modes, star range and the most played maps.

You can harvest as many profiles as you like, yours or other players'. Each one appears under **Beatmaps** in the sidebar; click a name to open it. Each profile keeps its own selection, and **Remove profile…** forgets one (maps you downloaded stay where they are).

If you are signed in, **My friends** lists your osu! friends with a **Harvest** button for each.

To look up a username made only of digits, put `@` in front of it (`@12345`); otherwise it is read as a user id.

### 2. Choose what to download

The **Maps** tab of a profile shows every map that was found. There are two ways to choose:

- **By filters**: everything that matches the filters is selected. You can filter by game mode, stars, length, how many times you played a difficulty, map status and where the map was found (best scores, first places, most played…). Click a map to add or remove it on top of the filters.
- **Hand-picked**: only the maps you click are selected.

Some useful extras:

- Click a cover to hear a preview of the song.
- Click **i** on a map (or a song in **Most played** on the Overview tab) to see its details: cover, BPM, length, ranked date, global plays, and every difficulty with its stars, CS, AR, OD, HP, max combo and your plays.
- Maps already in osu! are tagged **In osu!**, and maps you downloaded but haven't added to osu! yet are tagged **Downloaded**. Both are skipped unless you untick **Skip maps I already have**. The **Library** filter shows only maps that are in osu!, only downloaded, or not on this PC.
- Use the search box to find a song, artist, mapper or difficulty.
- **Outside the filters: Dim / Hide** chooses whether maps and difficulties that don't match the filters are shown faded or left out of the list.
- **Save selection…** and **Load selection…** keep your filters and choices in a file for later.

### 3. Download

Click **Download N maps**. The **Downloads** page shows the progress of each map. You can switch pages while it runs.

If you stop the download or close the app, start it again later: maps that were already downloaded are skipped, so it continues where it stopped.

### 4. Add the maps to osu!

- **osu!lazer**: on **Downloads**, click **Open downloads folder**, select the maps and drag them onto the osu!lazer window (or double-click them).
- **osu! (stable)**: on **Downloads**, click **Move downloads…**. This moves the `.osz` files into osu!'s Songs folder; the next time osu! starts, it imports them.

## osu! and osu!lazer

Beatmap Harvest detects both osu!lazer and osu! (stable). If you have both, it uses **osu!lazer** and tells you so; pick the other one in **Settings → osu!** if that's the one you play.

**osu! library** shows the maps already in the osu! you use and, for each harvested profile, how many of its maps are already there, only downloaded, or missing. **Show missing** opens that profile's beatmaps with only the maps you don't have.

Beatmap Harvest only reads osu!'s files, so osu! can stay open. With osu!lazer it reads the beatmap files in lazer's storage; with osu! (stable) it reads `osu!.db` and the Songs folder.

## Collections (osu! stable)

osu!lazer keeps collections and skins in its own database, which other apps can't change, so these two tools work with osu! (stable).

**Collections** shows your osu! collections and lets you create, rename and delete them and remove maps from them. On **Beatmaps**, **Add to collection…** adds every difficulty of the selected maps to a collection. Maps not in osu! yet can be included too: Beatmap Harvest looks them up on osu! (about one per second), and they appear in the collection as soon as you download them.

Changes stay in Beatmap Harvest until you click **Save to osu!**. Close osu! first, because osu! rewrites its collections when it exits. Each save keeps a copy of the previous file next to it (`collection.db.<date>.bak`).

## Skins (osu! stable)

**Skins** lists the skins installed in osu! with their author and a preview of their main elements (cursor, hit circles, rank letters, mania notes…), and marks the one in use.

## Settings

Settings are saved as soon as you change them. Each folder can be typed, picked with **Browse…**, or set back to its default with **Default**.

- **osu! → Use**: Automatic (osu!lazer when installed), osu!lazer, or osu! (stable).
- **osu!lazer folder** / **osu! (stable) folder**: detected automatically; set them only if osu! is somewhere unusual.
- **Songs folder** (stable): by default, the one osu! itself uses.
- **Downloads folder**: where `.osz` files are saved.
- **Move downloads to** (stable): where **Move downloads…** puts the files. By default, the Songs folder.
- **Parallel downloads**: how many maps download at the same time.

## FAQ

**Why do I need to create an OAuth application?**
It is how osu! lets third-party apps read public profile data. Each person uses their own, so no secret is shared inside the app.

**Where are the maps downloaded from?**
From public beatmap mirrors. Official osu! downloads require logging in to the website, and Beatmap Harvest never asks for your osu! password.

**Some maps failed to download.**
Mirrors don't always have every map, especially old or unranked ones. Try again later. Maps that already finished are not downloaded twice.

**I use osu!lazer. Does it detect the maps I already have?**
Yes. A map you deleted in lazer may still count as installed until lazer cleans up its storage.

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
