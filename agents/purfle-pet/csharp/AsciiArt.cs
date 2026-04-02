namespace PurflePet;

/// <summary>
/// ASCII art faces for each pet mood.
/// </summary>
public static class AsciiArt
{
    public static string Happy()    => @"(^‿^)";
    public static string Sad()      => @"(╥_╥)";
    public static string Hungry()   => @"(>_<)~";
    public static string Sleepy()   => @"(o_o)zzZ";
    public static string Excited()  => @"\(★ω★)/";

    /// <summary>
    /// Returns the ASCII art face for the given mood string.
    /// </summary>
    public static string ForMood(string mood) => mood.ToLowerInvariant() switch
    {
        "happy"   => Happy(),
        "sad"     => Sad(),
        "hungry"  => Hungry(),
        "sleepy"  => Sleepy(),
        "excited" => Excited(),
        _         => Happy()
    };
}
