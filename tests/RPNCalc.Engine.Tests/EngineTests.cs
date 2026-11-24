using RPNCalc.Engine;

namespace RPNCalc.Engine.Tests;

public class StackTests
{
    private static RpnCalculator Type(string keys)
    {
        var c = new RpnCalculator();
        Run(c, keys);
        return c;
    }

    private static string? Run(RpnCalculator c, string keys)
    {
        string? err = null;
        foreach (char k in keys)
        {
            var cmd = k == '\n' ? CalcCommand.Enter : CommandMap.FromChar(k);
            err = c.Execute(cmd) ?? err;
        }
        return err;
    }

    [Fact] public void TypingBuildsNumber() { var c = Type("12.5"); Assert.Equal(12.5, c.X); Assert.Equal("12.5", c.EntryText); }
    [Fact] public void LeadingZeroReplaced() => Assert.Equal("7", Type("007").EntryText);
    [Fact] public void LeadingDecimalGetsZero() => Assert.Equal("0.5", Type(".5").EntryText);
    [Fact] public void SecondDecimalIgnored() => Assert.Equal("1.23", Type("1.2.3").EntryText);
    [Fact] public void CommaIsDecimal() => Assert.Equal(1.5, Type("1,5").X);

    [Fact]
    public void EntryLimitedTo15Digits()
    {
        var c = Type("12345678901234567890");
        Assert.Equal(15, c.EntryText!.Length);
    }

    [Fact]
    public void EnterDuplicatesAndNextDigitOverwrites()
    {
        var c = Type("3\n");
        Assert.Equal(3, c.X); Assert.Equal(3, c.Y);
        Run(c, "4");
        Assert.Equal(4, c.X); Assert.Equal(3, c.Y); Assert.Equal(0, c.Z);
    }

    [Fact]
    public void EnterTwiceFillsStack()
    {
        var c = Type("5\n\n\n");
        Assert.Equal(5, c.X); Assert.Equal(5, c.Y); Assert.Equal(5, c.Z); Assert.Equal(5, c.T);
    }

    [Theory]
    [InlineData("3\n4+", 7)]
    [InlineData("10\n4-", 6)]
    [InlineData("6\n7*", 42)]
    [InlineData("1\n4/", 0.25)]
    public void BinaryOps(string keys, double expected) => Assert.Equal(expected, Type(keys).X, 12);

    [Fact]
    public void OperationDropsStackAndTDuplicates()
    {
        var c = Type("1\n2\n3\n4+");  // T=1 Z=2 Y=3 X=4
        Assert.Equal(7, c.X); Assert.Equal(2, c.Y); Assert.Equal(1, c.Z); Assert.Equal(1, c.T);
    }

    [Fact]
    public void DigitAfterOperationPushes()
    {
        var c = Type("3\n4+5");
        Assert.Equal(5, c.X); Assert.Equal(7, c.Y);
        Run(c, "*");
        Assert.Equal(35, c.X);
    }

    [Fact]
    public void ChainCalculation() => Assert.Equal(10, Type("2\n3+4\n2/*").X, 12); // (2+3)*(4/2)

    [Fact]
    public void LastXSavedBeforeOperation()
    {
        var c = Type("3\n4+");
        Assert.Equal(4, c.LastX);
        Run(c, "L");
        Assert.Equal(4, c.X); Assert.Equal(7, c.Y);
    }

    [Fact]
    public void LastXSavedBeforeConversion()
    {
        var c = Type("10");
        Assert.Null(c.Convert(Conversions.Defaults[^1].Factor));
        Assert.Equal(22, c.X, 12);
        Assert.Equal(10, c.LastX);
    }

    [Fact]
    public void DivideByZeroReportsErrorAndKeepsStack()
    {
        var c = Type("5\n0");
        Assert.Equal(RpnCalculator.ErrDivideByZero, c.Divide());
        Assert.Equal(0, c.X); Assert.Equal(5, c.Y);
    }

    [Fact] public void ReciprocalOfZero() { var c = Type("0"); Assert.Equal(RpnCalculator.ErrDivideByZero, c.Reciprocal()); }
    [Fact] public void SqrtNegative() { var c = Type("4S"); Assert.Equal(RpnCalculator.ErrInvalid, c.SquareRoot()); Assert.Equal(-4, c.X); }
    [Fact] public void SqrtPositive() { var c = Type("16"); Assert.Null(c.SquareRoot()); Assert.Equal(4, c.X); Assert.Equal(16, c.LastX); }
    [Fact] public void SquareWorks() { var c = Type("12"); c.Square(); Assert.Equal(144, c.X); }
    [Fact] public void Reciprocal() { var c = Type("8"); c.Reciprocal(); Assert.Equal(0.125, c.X); }
    [Fact] public void PowerYX() { var c = Type("2\n10"); Assert.Null(c.Power()); Assert.Equal(1024, c.X); }
    [Fact] public void PowerNegativeBaseFraction() { var c = Type("8S\n.5"); Assert.Equal(RpnCalculator.ErrInvalid, c.Power()); }
    [Fact] public void PowerZeroNegative() { var c = Type("0\n1S"); Assert.Equal(RpnCalculator.ErrDivideByZero, c.Power()); }
    [Fact] public void Overflow() { var c = Type("10\n400"); c.Execute(CalcCommand.D0); Assert.Equal(RpnCalculator.ErrOverflow, c.Power()); }

    [Fact]
    public void ChangeSignDuringEntryAndAfter()
    {
        var c = Type("5S");
        Assert.Equal("-5", c.EntryText);
        Run(c, "2");
        Assert.Equal(-52, c.X);
        Run(c, "\nS");
        Assert.Equal(52, c.X); Assert.Equal(-52, c.Y);
    }

    [Fact]
    public void ChangeSignOfZeroIsNotNegativeZero()
    {
        var c = new RpnCalculator(); c.ChangeSign();
        Assert.Equal("0.00", NumberFormatter.Format(c.X, 2, FormatType.Fixed));
    }

    [Fact]
    public void BackspaceDeletesDigits()
    {
        var c = Type("123");
        c.Backspace(); Assert.Equal(12, c.X);
        c.Backspace(); c.Backspace();
        Assert.False(c.IsEntering); Assert.Equal(0, c.X);
        Run(c, "9"); Assert.Equal(9, c.X); Assert.Equal(0, c.Y); // overwrote, no push
    }

    [Fact]
    public void BackspaceAfterResultClearsX()
    {
        var c = Type("3\n4+");
        c.Backspace();
        Assert.Equal(0, c.X);
        Run(c, "5"); Assert.Equal(5, c.X); Assert.Equal(0, c.Y);
    }

    [Fact]
    public void SwapExchangesXY()
    {
        var c = Type("1\n2X");
        Assert.Equal(1, c.X); Assert.Equal(2, c.Y);
    }

    [Fact]
    public void RollUpAndDown()
    {
        var c = Type("1\n2\n3\n4"); // T1 Z2 Y3 X4
        c.RollDown();
        Assert.Equal((3d, 2d, 1d, 4d), (c.X, c.Y, c.Z, c.T));
        c.RollUp();
        Assert.Equal((4d, 3d, 2d, 1d), (c.X, c.Y, c.Z, c.T));
        c.RollUp();
        Assert.Equal((1d, 4d, 3d, 2d), (c.X, c.Y, c.Z, c.T));
    }

    [Fact]
    public void PiPushesOrOverwritesAfterEnter()
    {
        var c = Type("2P");
        Assert.Equal(Math.PI, c.X); Assert.Equal(2, c.Y);
        var d = Type("2\nP");
        Assert.Equal(Math.PI, d.X); Assert.Equal(2, d.Y); Assert.Equal(0, d.Z);
    }

    [Fact]
    public void KeyMapCoversShortcuts()
    {
        Assert.Equal(CalcCommand.ChangeSign, CommandMap.FromChar('s'));
        Assert.Equal(CalcCommand.LastX, CommandMap.FromChar('L'));
        Assert.Equal(CalcCommand.Swap, CommandMap.FromChar('x'));
        Assert.Equal(CalcCommand.Pi, CommandMap.FromChar('p'));
        Assert.Equal(CalcCommand.D7, CommandMap.FromChar('7'));
        Assert.Equal(CalcCommand.None, CommandMap.FromChar('u'));
        Assert.Equal(CalcCommand.None, CommandMap.FromChar('d'));
    }

    [Fact]
    public void SetStateSanitizes()
    {
        var c = new RpnCalculator();
        c.SetState(double.NaN, 2, double.PositiveInfinity, 4, 5, true);
        Assert.Equal((0d, 2d, 0d, 4d, 5d), (c.X, c.Y, c.Z, c.T, c.LastX));
        Assert.True(c.StackLift);
    }
}

public class ConversionTests
{
    private static double F(string from, string to) => Conversions.Defaults.Single(c => c.From == from && c.To == to).Factor;

    [Fact]
    public void ExactFactors()
    {
        Assert.Equal(16.387064, F("Cubic Inches", "Milliliters"));
        Assert.Equal(29.5735295625, F("Fluid Ounces (US)", "Milliliters"));
        Assert.Equal(1.8046875, F("Fluid Ounces (US)", "Cubic Inches"));
        Assert.Equal(2.2, F("Grams", "lbs./M"));
        Assert.Equal(0.554112554, F("Cubic Inches", "Fluid Ounces (US)"), 9);
        Assert.Equal(0.0610237441, F("Milliliters", "Cubic Inches"), 10);
        Assert.Equal(0.0338140227, F("Milliliters", "Fluid Ounces (US)"), 10);
    }

    [Fact]
    public void RoundTripsAreConsistent()
    {
        Assert.Equal(1.0, F("Cubic Inches", "Fluid Ounces (US)") * F("Fluid Ounces (US)", "Cubic Inches"), 12);
        Assert.Equal(1.0, F("Milliliters", "Cubic Inches") * F("Cubic Inches", "Milliliters"), 12);
        Assert.Equal(1.0, F("Milliliters", "Fluid Ounces (US)") * F("Fluid Ounces (US)", "Milliliters"), 12);
    }

    [Fact]
    public void MenuOrderAndLabels()
    {
        Assert.Equal(7, Conversions.Defaults.Count);
        Assert.Equal("Grams -> lbs./M", Conversions.Defaults[^1].Name);
        Assert.Equal("Grams → lbs./M", Conversions.Defaults[^1].MenuText);
    }

    [Fact]
    public void DefaultCsvRoundTripsExactly()
    {
        string csv = Conversions.ToCsv(Conversions.Defaults);
        Assert.StartsWith("From,To,Factor\r\n", csv);
        Assert.Contains("Grams,lbs./M,2.2\r\n", csv);
        var (items, warnings) = Conversions.Parse(csv);
        Assert.Empty(warnings);
        Assert.Equal(Conversions.Defaults, items);   // record equality: names and bit-exact factors
    }

    [Fact]
    public void ParsesThreeColumnsQuotesCommentsAndBlankLines()
    {
        var (items, warnings) = Conversions.Parse(
            "\uFEFF# my list\n\nInches, Centimeters, 2.54\r\n\"Feet, US\",Meters,0.3048\n  Miles ,Kilometers,1.609344e0\n");
        Assert.Empty(warnings);
        Assert.Equal([new Conversion("Inches", "Centimeters", 2.54), new Conversion("Feet, US", "Meters", 0.3048),
                      new Conversion("Miles", "Kilometers", 1.609344)], items);
    }

    [Fact]
    public void HeaderOnlySkippedOnFirstLine()
    {
        var (items, warnings) = Conversions.Parse("Source,Target,Multiplier\nA,B,2\nC,D,x\n");
        Assert.Equal([new Conversion("A", "B", 2)], items);
        Assert.Equal(["Line 3: \"x\" is not a valid factor."], warnings);
    }

    [Theory]
    [InlineData("A,B", "expected 3 fields")]
    [InlineData("A,B,1,2", "expected 3 fields")]
    [InlineData(",B,1", "must not be empty")]
    [InlineData("A,B,0", "not a valid factor")]
    [InlineData("A,B,1,5", "expected 3 fields")]   // comma decimal must be quoted -> 4 fields
    [InlineData("A,B,NaN", "not a valid factor")]
    public void BadLinesAreSkippedWithAWarning(string line, string message)
    {
        var (items, warnings) = Conversions.Parse("Grams,lbs./M,2.2\n" + line);
        Assert.Single(items);
        Assert.Contains(message, Assert.Single(warnings));
        Assert.StartsWith("Line 2:", warnings[0]);
    }

    [Fact]
    public void LoadCreatesDefaultFileThenReadsEdits()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rpncalc_conv_" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Conversions.GetPath(Path.Combine(dir, "settings.json"));
            Assert.Equal(Path.Combine(dir, "conversions.csv"), path);
            var t = Conversions.Load(path);
            Assert.True(File.Exists(path));
            Assert.Equal(Conversions.Defaults, t.Items);

            File.WriteAllText(path, "From,To,Factor\nKilograms,Pounds,2.20462262185\n");
            t = Conversions.Load(path);
            Assert.Equal([new Conversion("Kilograms", "Pounds", 2.20462262185)], t.Items);
            Assert.Empty(t.Warnings);

            File.WriteAllText(path, "");
            Assert.Empty(Conversions.Load(path).Items);   // an emptied file is respected, not refilled
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void HelpSectionListsFileAndWarnings()
    {
        var t = new ConversionTable([new Conversion("Grams", "lbs./M", 2.2)], ["Line 3: bad"], "x");
        string help = Conversions.HelpSection(t, "%APPDATA%\\RPNCalc\\conversions.csv");
        Assert.Contains("Grams → lbs./M", help);
        Assert.Contains("× 2.2", help);
        Assert.Contains("Roman numeral", help);
        Assert.Contains("%APPDATA%\\RPNCalc\\conversions.csv", help);
        Assert.Contains("Line 3: bad", help);
    }
}

public class FormatterTests
{
    [Theory]
    [InlineData(3.14159265, 4, "3.1416")]
    [InlineData(3.14159265, 0, "3")]
    [InlineData(-2.5, 2, "-2.50")]
    [InlineData(1234567.891, 9, "1234567.891000000")]
    [InlineData(-0.0001, 2, "0.00")]
    [InlineData(0, 4, "0.0000")]
    public void Fixed(double v, int d, string expected) => Assert.Equal(expected, NumberFormatter.Format(v, d, FormatType.Fixed));

    [Theory]
    [InlineData(12345, 3, "1.235E+04")]
    [InlineData(0.00012345, 2, "1.23E-04")]
    [InlineData(-6.02e23, 2, "-6.02E+23")]
    [InlineData(5, 0, "5E+00")]
    [InlineData(0, 2, "0.00E+00")]
    public void Scientific(double v, int d, string expected) => Assert.Equal(expected, NumberFormatter.Format(v, d, FormatType.Scientific));

    [Fact] public void FixedFallsBackForHuge() => Assert.Equal("1.00E+20", NumberFormatter.Format(1e20, 2, FormatType.Fixed));
    [Fact] public void FixedFallsBackForTiny() => Assert.Equal("1.00E-12", NumberFormatter.Format(1e-12, 2, FormatType.Fixed));
    [Fact] public void DecimalsClamped() => Assert.Equal("1.000000000", NumberFormatter.Format(1, 42, FormatType.Fixed));
}

public class SettingsTests
{
    [Fact]
    public void JsonRoundTrip()
    {
        var s = new AppSettings
        {
            Left = 100, Top = -20, Width = 300, Decimals = 7, Format = FormatType.Scientific,
            ShowStack = false, ShowKeypad = false, StayOnTop = true, FontSize = 20,
            X = 1.5, Y = -2.25, Z = Math.PI, T = 1e300, LastX = 0.1, StackLift = true,
        };
        string json = SettingsStore.Serialize(s);
        Assert.Contains("\"Scientific\"", json);
        var r = SettingsStore.Deserialize(json);
        Assert.Equal(SettingsStore.Serialize(s), SettingsStore.Serialize(r));
        Assert.Equal(Math.PI, r.Z);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"Decimals\": \"abc\"}")]
    public void CorruptJsonGivesDefaults(string? json)
    {
        var r = SettingsStore.Deserialize(json);
        Assert.Equal(4, r.Decimals); Assert.True(r.ShowStack); Assert.Null(r.Left);
    }

    [Fact]
    public void OutOfRangeValuesClamped()
    {
        var r = SettingsStore.Deserialize("{\"Decimals\": 42, \"FontSize\": 2, \"Width\": 5, \"Format\": 9}");
        Assert.Equal(9, r.Decimals); Assert.Equal(AppSettings.MinFontSize, r.FontSize);
        Assert.Null(r.Width); Assert.Equal(FormatType.Fixed, r.Format);
    }

    [Fact]
    public void PartialJsonKeepsDefaults()
    {
        var r = SettingsStore.Deserialize("{\"X\": 42, /* comment */ \"ShowKeypad\": false,}");
        Assert.Equal(42, r.X); Assert.False(r.ShowKeypad); Assert.Equal(16, r.FontSize);
    }

    [Fact]
    public void DefaultPathIsPlatformSpecific()
    {
        string win = SettingsStore.GetDefaultPath(macOS: false);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.DoNotVerify), "RPNCalc", "settings.json"), win);
        string mac = SettingsStore.GetDefaultPath(macOS: true);
        // Never a relative path, even when the folder does not exist yet (fresh profile).
        Assert.True(Path.IsPathRooted(win), win);
        Assert.True(Path.IsPathRooted(mac), mac);
        Assert.EndsWith(Path.Combine("Library", "Application Support", "RPNCalc", "settings.json"), mac);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), mac);
        Assert.Equal(SettingsStore.GetDefaultPath(OperatingSystem.IsMacOS()), SettingsStore.DefaultPath);
    }

    [Fact]
    public void SaveAndLoadFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rpncalc_test_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "sub", "settings.json");
        try
        {
            Assert.Equal(4, SettingsStore.Load(path).Decimals); // missing file -> defaults
            Assert.True(SettingsStore.Save(new AppSettings { Decimals = 2, Y = 9 }, path));
            var r = SettingsStore.Load(path);
            Assert.Equal(2, r.Decimals); Assert.Equal(9, r.Y);
            File.WriteAllText(path, "garbage");
            Assert.Equal(4, SettingsStore.Load(path).Decimals);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
