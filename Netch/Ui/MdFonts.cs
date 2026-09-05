namespace Netch.Ui;

internal static class MdFonts
{
    private static readonly string[] Families =
    {
        "Microsoft YaHei UI",
        "Segoe UI Variable Text",
        "Segoe UI",
        "微软雅黑"
    };

    public static readonly Font Body = Create(9.75f);
    public static readonly Font Label = Create(9f, FontStyle.Bold);

    public static Font Create(float points, FontStyle style = FontStyle.Regular)
    {
        foreach (var name in Families)
        {
            using var probe = new Font(name, points, style, GraphicsUnit.Point);
            if (string.Equals(probe.Name, name, StringComparison.OrdinalIgnoreCase))
                return new Font(name, points, style, GraphicsUnit.Point);
        }

        return new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, style);
    }
}
