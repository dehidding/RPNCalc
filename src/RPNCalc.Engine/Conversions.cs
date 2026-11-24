using System.Globalization;
using System.Text;

namespace RPNCalc.Engine;

/// <summary>One conversion: X (in <see cref="From"/> units) is multiplied by <see cref="Factor"/> to give <see cref="To"/> units.</summary>
public sealed record Conversion(string From, string To, double Factor)
{
    public string Name => $"{From} -> {To}";
    public string MenuText => $"{From} → {To}";
}

/// <summary>Result of reading a conversions CSV: the valid rows plus a message for every skipped line.</summary>
public sealed record ConversionTable(IReadOnlyList<Conversion> Items, IReadOnlyList<string> Warnings, string Path);

/// <summary>
/// Conversions are read from a 3-column CSV file (From, To, Factor) next to settings.json:
///   Windows: %APPDATA%\RPNCalc\conversions.csv
///   macOS:   ~/Library/Application Support/RPNCalc/conversions.csv
/// The file is created from the built-in default list if it does not exist.
/// </summary>
public static class Conversions
{
    public const string FileName = "conversions.csv";
    public const double MlPerCubicInch = 16.387064;        // exact (2.54^3)
    public const double MlPerFluidOunce = 29.5735295625;   // exact (US fl oz = 1.8046875 in³)

    /// <summary>
    /// Text of the default conversions.csv (src/RPNCalc.Engine/conversions.csv, embedded in the app).
    /// It is written verbatim when the user's file does not exist yet.
    /// </summary>
    public static string DefaultCsv { get; } = ReadDefaultCsv();

    /// <summary>The default conversions, in file order (parsed from <see cref="DefaultCsv"/>).</summary>
    public static IReadOnlyList<Conversion> Defaults { get; } = Parse(DefaultCsv).Items;

    private static string ReadDefaultCsv()
    {
        using var s = typeof(Conversions).Assembly.GetManifestResourceStream("RPNCalc.Engine.conversions.csv")
            ?? throw new InvalidOperationException("Embedded conversions.csv is missing.");
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    /// <summary>conversions.csv in the same folder as the given (or default) settings.json.</summary>
    public static string GetPath(string? settingsPath = null) =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsPath ?? SettingsStore.DefaultPath)!, FileName);

    /// <summary>CSV text for a list of conversions (header row, round-trip factors).</summary>
    public static string ToCsv(IEnumerable<Conversion> items)
    {
        var sb = new StringBuilder("From,To,Factor\r\n");
        foreach (var c in items)
            sb.Append(Quote(c.From)).Append(',').Append(Quote(c.To)).Append(',')
              .Append(c.Factor.ToString("R", CultureInfo.InvariantCulture)).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// Parse CSV text. Blank lines and lines starting with # are ignored; a first line whose third field
    /// is not a number is treated as a header. Fields may be quoted ("a, b"). Factors use a "." decimal point.
    /// </summary>
    public static (List<Conversion> Items, List<string> Warnings) Parse(string? csv)
    {
        var items = new List<Conversion>();
        var warnings = new List<string>();
        if (string.IsNullOrEmpty(csv)) return (items, warnings);

        string[] lines = csv.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        bool firstDataLine = true;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int lineNo = i + 1;
            var f = SplitCsvLine(line);
            bool numeric = f.Count >= 3 && TryParseFactor(f[2], out _);
            if (firstDataLine && !numeric && f.Count >= 3) { firstDataLine = false; continue; }   // header
            firstDataLine = false;

            if (f.Count != 3) { warnings.Add($"Line {lineNo}: expected 3 fields (From,To,Factor), found {f.Count}."); continue; }
            if (f[0].Length == 0 || f[1].Length == 0) { warnings.Add($"Line {lineNo}: From and To must not be empty."); continue; }
            if (!TryParseFactor(f[2], out double factor)) { warnings.Add($"Line {lineNo}: \"{f[2]}\" is not a valid factor."); continue; }
            items.Add(new Conversion(f[0], f[1], factor));
        }
        return (items, warnings);
    }

    /// <summary>
    /// Read the CSV file. If it does not exist it is created with <see cref="Defaults"/>.
    /// If it cannot be read, the defaults are used and a warning is returned.
    /// </summary>
    public static ConversionTable Load(string? path = null)
    {
        path ??= GetPath();
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.WriteAllText(path, DefaultCsv, new UTF8Encoding(false));
            }
            var (items, warnings) = Parse(File.ReadAllText(path));
            return new ConversionTable(items, warnings, path);
        }
        catch (Exception ex)
        {
            return new ConversionTable(Defaults, [$"Could not read {path}: {ex.Message} Using the built-in conversions."], path);
        }
    }

    /// <summary>The CONVERSIONS section of the Help text (same for Windows and macOS).</summary>
    public static string HelpSection(ConversionTable table, string displayPath)
    {
        var sb = new StringBuilder();
        if (table.Items.Count == 0) sb.AppendLine("  (none: the file has no valid conversions)");
        foreach (var c in table.Items)
            sb.AppendLine($"  {c.MenuText,-36} × {c.Factor.ToString("0.##########", CultureInfo.InvariantCulture)}");
        if (table.Items.Any(c => c.From.Contains("lbs./M") || c.To.Contains("lbs./M")))
            sb.AppendLine("""
                  "lbs./M": grams per piece → pounds per M pieces
                  ("M" is the Roman numeral for 1,000; factor 2.2 as in 1997).
                """.TrimEnd());
        sb.AppendLine();
        sb.AppendLine($$"""
            The Conversions menu is built from a CSV file with three
            columns, From,To,Factor (X in From units × Factor = To):
              {{displayPath}}
            Example line:   Grams,lbs./M,2.2
            Use "." as the decimal point and put a name that contains a
            comma in quotes. Blank lines, lines starting with # and the
            From,To,Factor header are ignored. Edits take effect the next
            time you open the menu; Conversions → Edit Conversions...
            opens the file. Delete it to get the built-in list back.
            """.TrimEnd());
        if (table.Warnings.Count > 0)
        {
            sb.AppendLine().AppendLine("Skipped lines in the file:");
            foreach (var w in table.Warnings) sb.AppendLine("  " + w);
        }
        return sb.ToString().TrimEnd();
    }

    private static bool TryParseFactor(string s, out double v) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && double.IsFinite(v) && v != 0;

    private static string Quote(string s) =>
        s.IndexOfAny([',', '"', '\n', '\r']) >= 0 || s != s.Trim() ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false, quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else if (c == '"' && sb.ToString().Trim().Length == 0) { sb.Clear(); inQuotes = quoted = true; }
            else if (c == ',') { fields.Add(quoted ? sb.ToString() : sb.ToString().Trim()); sb.Clear(); quoted = false; }
            else if (!(quoted && char.IsWhiteSpace(c))) sb.Append(c);
        }
        fields.Add(quoted ? sb.ToString() : sb.ToString().Trim());
        return fields;
    }
}
