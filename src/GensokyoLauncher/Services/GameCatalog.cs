using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public static class GameCatalog
{
    public static IReadOnlyList<GameDefinition> All { get; } =
    [
        new("th01", "Touhou 1 — Highly Responsive to Prayers", GameCategory.Pc98, "th01", "reiiden", "hrtp", "highly responsive to prayers"),
        new("th02", "Touhou 2 — Story of Eastern Wonderland", GameCategory.Pc98, "th02", "fuumaroku", "soew", "story of eastern wonderland"),
        new("th03", "Touhou 3 — Phantasmagoria of Dim.Dream", GameCategory.Pc98, "th03", "yumejikuu", "podd", "phantasmagoria of dim dream"),
        new("th04", "Touhou 4 — Lotus Land Story", GameCategory.Pc98, "th04", "gensokyo", "lls", "lotus land story"),
        new("th05", "Touhou 5 — Mystic Square", GameCategory.Pc98, "th05", "kaikidan", "ms", "mystic square"),
        new("th06", "Touhou 6 — The Embodiment of Scarlet Devil", GameCategory.Mainline, "th06 (thpatch-en).exe", "th06.exe"),
        new("th07", "Touhou 7 — Perfect Cherry Blossom", GameCategory.Mainline, "th07.exe"),
        new("th075", "Touhou 7.5 — Immaterial and Missing Power", GameCategory.OffMainline, "th075.exe"),
        new("th08", "Touhou 8 — Imperishable Night", GameCategory.Mainline, "th08.exe"),
        new("th09", "Touhou 9 — Phantasmagoria of Flower View", GameCategory.Mainline, "th09.exe"),
        new("th095", "Touhou 9.5 — Shoot the Bullet", GameCategory.Mainline, "th095.exe"),
        new("th10", "Touhou 10 — Mountain of Faith", GameCategory.Mainline, "th10.exe"),
        new("th105", "Touhou 10.5 — Scarlet Weather Rhapsody", GameCategory.OffMainline, "th105.exe"),
        new("th11", "Touhou 11 — Subterranean Animism", GameCategory.Mainline, "th11.exe"),
        new("th12", "Touhou 12 — Undefined Fantastic Object", GameCategory.Mainline, "th12.exe"),
        new("th123", "Touhou 12.3 — Hisoutensoku", GameCategory.OffMainline, "th123.exe"),
        new("th125", "Touhou 12.5 — Double Spoiler", GameCategory.Mainline, "th125.exe"),
        new("th128", "Touhou 12.8 — Great Fairy Wars", GameCategory.Mainline, "th128.exe"),
        new("th13", "Touhou 13 — Ten Desires", GameCategory.Mainline, "th13.exe"),
        new("th135", "Touhou 13.5 — Hopeless Masquerade", GameCategory.OffMainline, "th135.exe"),
        new("th14", "Touhou 14 — Double Dealing Character", GameCategory.Mainline, "th14.exe"),
        new("th143", "Touhou 14.3 — Impossible Spell Card", GameCategory.Mainline, "th143.exe"),
        new("th145", "Touhou 14.5 — Urban Legend in Limbo", GameCategory.OffMainline, "th145.exe"),
        new("th15", "Touhou 15 — Legacy of Lunatic Kingdom", GameCategory.Mainline, "th15.exe"),
        new("th155", "Touhou 15.5 — Antinomy of Common Flowers", GameCategory.OffMainline, "th155.exe"),
        new("th16", "Touhou 16 — Hidden Star in Four Seasons", GameCategory.Mainline, "th16.exe"),
        new("th165", "Touhou 16.5 — Violet Detector", GameCategory.Mainline, "th165.exe"),
        new("th17", "Touhou 17 — Wily Beast and Weakest Creature", GameCategory.Mainline, "th17.exe"),
        new("th175", "Touhou 17.5 — Sunken Fossil World", GameCategory.OffMainline, "th175.exe"),
        new("th18", "Touhou 18 — Unconnected Marketeers", GameCategory.Mainline, "th18.exe"),
        new("th185", "Touhou 18.5 — 100th Black Market", GameCategory.Mainline, "th185.exe"),
        new("th19", "Touhou 19 — Unfinished Dream of All Living Ghost", GameCategory.Mainline, "th19.exe"),
        new("th20", "Touhou 20 — Fossilized Wonders", GameCategory.Mainline, "th20.exe")
    ];
}
