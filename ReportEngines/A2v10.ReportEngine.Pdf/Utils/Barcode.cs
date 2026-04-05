// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text;

// ── Options ───────────────────────────────────────────────────────────────────

/// <summary>
/// Rendering options passed to the barcode generator.
/// </summary>
class BarcodeOptions
{
    /// <summary>Height of the barcode bars in pixels. Default: 100.</summary>
    public Int32 Height { get; set; } = 100;

    /// <summary>Bar color as a CSS color string. Default: "#000000".</summary>
    public String BarColor { get; set; } = "#000000";

    /// <summary>Whether to render a transparent background. Default: true.</summary>
    public Boolean Transparent { get; set; } = true;

    /// <summary>Whether to print digit labels below the bars. Default: true.</summary>
    public Boolean PrintDigits { get; set; } = true;
}

// ── Generator ─────────────────────────────────────────────────────────────────

/// <summary>
/// Generates EAN-8 and EAN-13 barcodes as SVG strings.
/// Call GenerateEan13(code) or GenerateEan8(code) with 7–8 or 12–13 digit strings.
/// Check digits are computed automatically when omitted.
/// </summary>
class EanBarcodeGenerator(BarcodeOptions? _options = null)
{
    // ── Encoding tables ───────────────────────────────────────────────────────

    // L-code (odd parity) — used in left half of EAN-13 and both halves of EAN-8
    static readonly String[] L = [
        "0001101","0011001","0010011","0111101","0100011",
        "0110001","0101111","0111011","0110111","0001011"
    ];

    // G-code (even parity, mirror of R) — used in EAN-13 left half only
    static readonly String[] G = [
        "0100111","0110011","0011011","0100001","0011101",
        "0111001","0000101","0010001","0001001","0010111"
    ];

    // R-code (right half) — used in right half of both EAN-13 and EAN-8
    static readonly String[] R = [
        "1110010","1100110","1101100","1000010","1011100",
        "1001110","1010000","1000100","1001000","1110100"
    ];

    // Parity patterns for the EAN-13 left group (indexed by first digit)
    static readonly String[] Parity13 = [
        "LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG",
        "LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL"
    ];

    // ── SVG constants ─────────────────────────────────────────────────────────

    const Int32 ModuleWidth = 2;
    const Int32 GuardExtra = 5;     // guard bars extend below regular bars
    const Int32 QuietZone = 11 * ModuleWidth;
    const Int32 FontSize = 14;
    const Int32 PaddingTop = 4;
    const Int32 DigitAreaH = 22;
    const Int32 PaddingBot = 4;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates an EAN-13 barcode SVG.
    /// Accepts 12 digits (check digit computed) or 13 digits (check digit verified).
    /// </summary>
    public String GenerateEan13(String code)
    {
        var opt = _options ?? new BarcodeOptions();
        Int32[] d = Prepare(code, length: 12, CalcCheckDigit13);
        return BuildSvg(Encode13(d), opt, (sb, digitY) =>
        {
            if (!opt.PrintDigits) return;

            Int32 leftGroupX = QuietZone + 3 * ModuleWidth;
            Int32 middleGuardX = leftGroupX + 42 * ModuleWidth;
            Int32 rightGroupX = middleGuardX + 5 * ModuleWidth;

            // First digit — in the left quiet zone
            sb.AppendLine(DigitText(QuietZone / 2, digitY, d[0], opt.BarColor));

            // Left group: digits[1..6], each 7 modules wide
            for (Int32 i = 0; i < 6; i++)
            {
                Int32 cx = leftGroupX + i * 7 * ModuleWidth + 7 * ModuleWidth / 2;
                sb.AppendLine(DigitText(cx, digitY, d[i + 1], opt.BarColor));
            }

            // Right group: digits[7..12], each 7 modules wide
            for (Int32 i = 0; i < 6; i++)
            {
                Int32 cx = rightGroupX + i * 7 * ModuleWidth + 7 * ModuleWidth / 2;
                sb.AppendLine(DigitText(cx, digitY, d[i + 7], opt.BarColor));
            }
        });
    }

    /// <summary>
    /// Generates an EAN-8 barcode SVG.
    /// Accepts 7 digits (check digit computed) or 8 digits (check digit verified).
    /// </summary>
    public String GenerateEan8(String code)
    {
        var opt = _options ?? new BarcodeOptions();
        Int32[] d = Prepare(code, length: 7, CalcCheckDigit8);
        return BuildSvg(Encode8(d), opt, (sb, digitY) =>
        {
            if (!opt.PrintDigits) return;

            Int32 leftGroupX = QuietZone + 3 * ModuleWidth;
            Int32 middleGuardX = leftGroupX + 28 * ModuleWidth;
            Int32 rightGroupX = middleGuardX + 5 * ModuleWidth;

            // Left group: digits[0..3], each 7 modules wide
            for (Int32 i = 0; i < 4; i++)
            {
                Int32 cx = leftGroupX + i * 7 * ModuleWidth + 7 * ModuleWidth / 2;
                sb.AppendLine(DigitText(cx, digitY, d[i], opt.BarColor));
            }

            // Right group: digits[4..7], each 7 modules wide
            for (Int32 i = 0; i < 4; i++)
            {
                Int32 cx = rightGroupX + i * 7 * ModuleWidth + 7 * ModuleWidth / 2;
                sb.AppendLine(DigitText(cx, digitY, d[i + 4], opt.BarColor));
            }
        });
    }

    // ── Check digits ──────────────────────────────────────────────────────────

    // EAN-13: weights 1,3,1,3,... for indices 0..11
    static Int32 CalcCheckDigit13(Int32[] d)
    {
        Int32 sum = 0;
        for (Int32 i = 0; i < 12; i++)
            sum += d[i] * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }

    // EAN-8: weights 3,1,3,1,... for indices 0..6 (first digit gets ×3)
    static Int32 CalcCheckDigit8(Int32[] d)
    {
        Int32 sum = 0;
        for (Int32 i = 0; i < 7; i++)
            sum += d[i] * (i % 2 == 0 ? 3 : 1);
        return (10 - sum % 10) % 10;
    }

    // ── Bit-string encoding ───────────────────────────────────────────────────

    static String Encode13(Int32[] digits)
    {
        var sb = new StringBuilder();
        String parity = Parity13[digits[0]];

        sb.Append("101"); // start guard
        for (Int32 i = 0; i < 6; i++)
            sb.Append(parity[i] == 'L' ? L[digits[i + 1]] : G[digits[i + 1]]);
        sb.Append("01010"); // middle guard
        for (Int32 i = 7; i < 13; i++)
            sb.Append(R[digits[i]]);
        sb.Append("101"); // end guard

        return sb.ToString();
    }

    static String Encode8(Int32[] digits)
    {
        var sb = new StringBuilder();

        sb.Append("101"); // start guard
        for (Int32 i = 0; i < 4; i++)
            sb.Append(L[digits[i]]);
        sb.Append("01010"); // middle guard
        for (Int32 i = 4; i < 8; i++)
            sb.Append(R[digits[i]]);
        sb.Append("101"); // end guard

        return sb.ToString();
    }

    // ── SVG builder ───────────────────────────────────────────────────────────

    static String BuildSvg(String bars, BarcodeOptions opt, Action<StringBuilder, Int32> writeDigits)
    {
        Int32 barcodeH = opt.Height;
        Int32 digitAreaH = opt.PrintDigits ? DigitAreaH : 0;
        Int32 totalWidth = bars.Length * ModuleWidth + QuietZone * 2;
        Int32 totalHeight = PaddingTop + barcodeH + GuardExtra + digitAreaH + PaddingBot;

        var sb = new StringBuilder();
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{totalWidth}" height="{totalHeight}" viewBox="0 0 {totalWidth} {totalHeight}">""");

        if (!opt.Transparent)
            sb.AppendLine($"""  <rect width="{totalWidth}" height="{totalHeight}" fill="white"/>""");

        // ── Bars ──
        Int32 startGuardEnd = 3;
        Int32 middleGuardBeg = (bars.Length - 5) / 2;   // works for both EAN-8 and EAN-13
        Int32 middleGuardEnd = middleGuardBeg + 5;
        Int32 endGuardBeg = bars.Length - 3;

        Int32 x = QuietZone;
        for (Int32 i = 0; i < bars.Length; i++)
        {
            if (bars[i] == '1')
            {
                Boolean isGuard = i < startGuardEnd
                               || (i >= middleGuardBeg && i < middleGuardEnd)
                               || i >= endGuardBeg;

                Int32 h = isGuard ? barcodeH + GuardExtra : barcodeH;
                sb.AppendLine($"""  <rect x="{x}" y="{PaddingTop}" width="{ModuleWidth}" height="{h}" fill="{opt.BarColor}"/>""");
            }
            x += ModuleWidth;
        }

        // ── Digits ──
        Int32 digitY = PaddingTop + barcodeH + GuardExtra + FontSize;
        writeDigits(sb, digitY);

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="code"/> into a digit array of <paramref name="length"/>+1 elements.
    /// If the input has <paramref name="length"/> digits, the check digit is appended.
    /// If it has <paramref name="length"/>+1 digits, the check digit is verified.
    /// </summary>
    static Int32[] Prepare(String code, Int32 length, Func<Int32[], Int32> calcCheck)
    {
        Int32[] d = ToDigits(code);

        if (d.Length == length)
        {
            Int32 check = calcCheck(d);
            Array.Resize(ref d, length + 1);
            d[length] = check;
        }
        else if (d.Length == length + 1)
        {
            Int32 expected = calcCheck(d);
            if (d[length] != expected)
                throw new ArgumentException(
                    $"Invalid check digit: expected {expected}, got {d[length]}.");
        }
        else
        {
            throw new ArgumentException(
                $"Expected {length} or {length + 1} digits, got {d.Length}.");
        }

        return d;
    }

    static Int32[] ToDigits(String s)
    {
        var d = new Int32[s.Length];
        for (Int32 i = 0; i < s.Length; i++) d[i] = s[i] - '0';
        return d;
    }

    static String DigitText(Int32 x, Int32 y, Int32 digit, String color) =>
        $"""  <text x="{x}" y="{y}" text-anchor="middle" font-family="monospace" font-size="{FontSize}" fill="{color}">{digit}</text>""";
}

