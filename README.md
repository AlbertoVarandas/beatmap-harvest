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

Your client secret works like a password: don't share it. Beatmap Harvest stores it encrypted, readable only by your Windows account, and only ever sends it to osu.ppy.sh. It never asks for your osu! password.

## How to use

### 1. Harvest a profile

On **Harvest**, enter a profile link (`https://osu.ppy.sh/users/123`), a user id or a username, then click **Start harvest**.

You will see a summary: how many maps were found, your plays, game modes, star range and your most played maps. A large profile takes a minute or two, because osu! limits how fast the app can ask for data.

### 2. Choose what to download

**Select** shows every map that was found. There are two ways to choose:

- **By filters**: everything that matches the filters is selected. You can filter by game mode, stars, length, how many times you played a difficulty, map status and where the map was found (best scores, first places, most played…). Click a map to add or remove it on top of the filters.
- **Hand-picked**: only the maps you click are selected.

Some useful extras:

- Click a cover to hear a preview of the song.
- Maps you already have are tagged **Have it** and skipped. To include them anyway, untick **Skip maps I already have**.
- Use the search box to find a song, artist, mapper or difficulty.
- **Save selection…** and **Load selection…** keep your filters and choices in a file for later.

### 3. Download

Click **Download N maps**. The **Downloads** page shows the progress of each map. You can switch pages while it runs.

If you stop the download or close the app, start it again later: maps that were already downloaded are skipped, so it continues where it stopped.

### 4. Add the maps to osu!

On **Downloads**, click **Move to osu! Songs…**. The next time osu! starts, it imports them.

You can also open the downloads folder (**Open folder**) and double-click the `.osz` files, or drag them onto the osu! window.

## Settings

- **Downloads folder**: where `.osz` files are saved.
- **osu! Songs folder**: used to find maps you already have and as the destination of **Move to osu! Songs**. The default is the usual osu! install location.
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
