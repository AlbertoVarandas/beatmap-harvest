using System.Globalization;

namespace BeatmapHarvest.Core.Display;

/// <summary>osu!'s difficulty colour: a gamma-corrected gradient over the star rating, as on osu.ppy.sh.</summary>
public static class StarColor
{
    private const double Gamma = 2.2;

    private static readonly (double Stars, int Rgb)[] Stops =
    [
        (0.1, 0x4290fb),
        (1.25, 0x4fc0ff),
        (2, 0x4fffd5),
        (2.5, 0x7cff4f),
        (3.3, 0xf6f05c),
        (4.2, 0xff8068),
        (4.9, 0xff4e6f),
        (5.8, 0xc645b8),
        (6.7, 0x6563de),
        (7.7, 0x18158e),
        (9, 0x000000),
    ];

    /// <summary>The background colour for a star rating, as <c>#rrggbb</c>.</summary>
    public static string For(double stars)
    {
        if (stars < Stops[0].Stars)
            return "#aaaaaa";
        if (stars >= Stops[^1].Stars)
            return Hex(Stops[^1].Rgb);

        var i = 1;
        while (stars > Stops[i].Stars)
            i++;
        var (fromStars, from) = Stops[i - 1];
        var (toStars, to) = Stops[i];
        var t = (stars - fromStars) / (toStars - fromStars);
        return Hex((Channel(from, to, 16, t) << 16) | (Channel(from, to, 8, t) << 8) | Channel(from, to, 0, t));
    }

    /// <summary>Text drawn on <see cref="For"/>: dark on the light part of the gradient, osu!'s yellow on the dark part.</summary>
    public static string TextFor(double stars) => stars >= 6.5 ? "#ffd966" : "#000000";

    private static int Channel(int from, int to, int shift, double t)
    {
        var a = Math.Pow(((from >> shift) & 0xff) / 255.0, Gamma);
        var b = Math.Pow(((to >> shift) & 0xff) / 255.0, Gamma);
        return (int)Math.Round(Math.Pow(a + (t * (b - a)), 1 / Gamma) * 255);
    }

    private static string Hex(int rgb) => "#" + rgb.ToString("x6", CultureInfo.InvariantCulture);
}
