
// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.ReportEngine.Script;

namespace Test.PdfReportEngine;

[TestClass]
[TestCategory("Spell Currency (EN)")]
public class SpellCurrencyEN
{
    [TestMethod]
    public void SimpleUAH()
    {
        //var culture = CultureInfo.CreateSpecificCulture("uk-UA");

        var dict = new Dictionary<Decimal, String>()
        {
            { 152,      "One hundred fifty-two hryvnias and 00 kopecks" },
            { 1000.01M, "One thousand hryvnias and 01 kopeck" },
            { 1782.7M,  "One thousand seven hundred eighty-two hryvnias and 70 kopecks"},
            { 1.02M,    "One hryvnia and 02 kopecks" },
            { 2,        "Two hryvnias and 00 kopecks"},
            { 0.71M,    "Zero hryvnias and 71 kopecks"},
            { 219,      "Two hundred nineteen hryvnias and 00 kopecks"},
            { 10.05M,   "Ten hryvnias and 05 kopecks"},
            { 18,   "Eighteen hryvnias and 00 kopecks"},
            { 400,  "Four hundred hryvnias and 00 kopecks"},
            { 401,  "Four hundred one hryvnias and 00 kopecks"},
            { 402,  "Four hundred two hryvnias and 00 kopecks"},
            { 2000000,  "Two million hryvnias and 00 kopecks"},
            { 1000000000,  "One billion hryvnias and 00 kopecks"},
            { 7000000,  "Seven million hryvnias and 00 kopecks"},
            { 1782529, "One million seven hundred eighty-two thousand five hundred twenty-nine hryvnias and 00 kopecks"},
            { 1782524, "One million seven hundred eighty-two thousand five hundred twenty-four hryvnias and 00 kopecks"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.SpellCurrencyEn(item.Key, "980"));
        }
    }

    [TestMethod]
    public void SimpleUSD()
    {
        var dict = new Dictionary<Decimal, String>()
        {
            { 152,      "One hundred fifty-two dollars and 00 cents" },
            { 1000.01M, "One thousand dollars and 01 cent" },
            { 1782.7M,  "One thousand seven hundred eighty-two dollars and 70 cents"},
            { 1.02M,    "One dollar and 02 cents" },
            { 2,        "Two dollars and 00 cents"},
            { 0.71M,    "Zero dollars and 71 cents"},
            { 219,      "Two hundred nineteen dollars and 00 cents"},
            { 10.05M,   "Ten dollars and 05 cents"},
            { 18,   "Eighteen dollars and 00 cents"},
            { 400,  "Four hundred dollars and 00 cents"},
            { 401,  "Four hundred one dollars and 00 cents"},
            { 402,  "Four hundred two dollars and 00 cents"},
            { 2000000,  "Two million dollars and 00 cents"},
            { 1000000000,  "One billion dollars and 00 cents"},
            { 7000000,  "Seven million dollars and 00 cents"},
            { 1782529, "One million seven hundred eighty-two thousand five hundred twenty-nine dollars and 00 cents"},
            { 1782524, "One million seven hundred eighty-two thousand five hundred twenty-four dollars and 00 cents"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.SpellCurrencyEn(item.Key, "840"));
        }
    }

    [TestMethod]
    public void SimpleEUR()
    {
        var dict = new Dictionary<Decimal, String>()
        {
            { 152,      "One hundred fifty-two euro and 00 cents" },
            { 1000.01M, "One thousand euro and 01 cent" },
            { 1782.7M,  "One thousand seven hundred eighty-two euro and 70 cents"},
            { 1.02M,    "One euro and 02 cents" },
            { 2,        "Two euro and 00 cents"},
            { 0.71M,    "Zero euro and 71 cents"},
            { 219,      "Two hundred nineteen euro and 00 cents"},
            { 10.05M,   "Ten euro and 05 cents"},
            { 18,   "Eighteen euro and 00 cents"},
            { 400,  "Four hundred euro and 00 cents"},
            { 401,  "Four hundred one euro and 00 cents"},
            { 402,  "Four hundred two euro and 00 cents"},
            { 2000000,  "Two million euro and 00 cents"},
            { 1000000000,  "One billion euro and 00 cents"},
            { 7000000,  "Seven million euro and 00 cents"},
            { 1782529, "One million seven hundred eighty-two thousand five hundred twenty-nine euro and 00 cents"},
            { 1782524, "One million seven hundred eighty-two thousand five hundred twenty-four euro and 00 cents"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.SpellCurrencyEn(item.Key, "978"));
        }
    }
}