using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPNCalc.Engine;

/// <summary>Everything persisted between sessions (%APPDATA%\RPNCalc\settings.json).</summary>
public sealed class AppSettings
{
    public const int MinFontSize = 8, MaxFontSize = 36, DefaultFontSize = 16;

    // Window (Left/Top in screen pixels; Width in 96-DPI logical units). Null = let Windows choose.
    public int? Left { get; set; }
    public int? Top { get; set; }
    public int? Width { get; set; }

    // Options
    public int Decimals { get; set; } = 4;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormatType Format { get; set; } = FormatType.Fixed;
    public bool ShowStack { get; set; } = true;
    public bool ShowKeypad { get; set; } = true;
    public bool StayOnTop { get; set; }
    public int FontSize { get; set; } = DefaultFontSize;

    // Stack
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double T { get; set; }
    public double LastX { get; set; }
    public bool StackLift { get; set; }

    /// <summary>Clamp out-of-range values (hand-edited or corrupt file).</summary>
    public AppSettings Normalize()
    {
        Decimals = Math.Clamp(Decimals, 0, 9);
        FontSize = Math.Clamp(FontSize, MinFontSize, MaxFontSize);
        if (!Enum.IsDefined(Format)) Format = FormatType.Fixed;
        if (Width is < 50 or > 10000) Width = null;
        X = Fin(X); Y = Fin(Y); Z = Fin(Z); T = Fin(T); LastX = Fin(LastX);
        return this;
    }

    private static double Fin(double v) => double.IsFinite(v) ? v : 0;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPNCalc", "settings.json");

    public static string Serialize(AppSettings s) => JsonSerializer.Serialize(s, Options);

    /// <summary>Parse JSON; returns defaults on any error.</summary>
    public static AppSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
        try { return (JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings()).Normalize(); }
        catch (Exception) { return new AppSettings(); }
    }

    /// <summary>Load from disk; missing, unreadable or corrupt file yields defaults.</summary>
    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try { return File.Exists(path) ? Deserialize(File.ReadAllText(path)) : new AppSettings(); }
        catch (Exception) { return new AppSettings(); }
    }

    /// <summary>Save atomically (write temp file then replace). Returns false on failure.</summary>
    public static bool Save(AppSettings s, string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Serialize(s));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception) { return false; }
    }
}
