// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System.Globalization;

using A2v10.ReportEngine.Script;

namespace Test.PdfReportEngine;

[TestClass]
[TestCategory("Spell Numbers")]
public class SpellNumbers
{
    [TestMethod]
    public void SimpleUA()
    {
        var culture = CultureInfo.CreateSpecificCulture("uk-UA");

        var dict = new Dictionary<Decimal, String>()
        {
            { 152, "сто п’ятдесят дві" },
            { 1000, "одна тисяча" },
            { 1782, "одна тисяча сімсот вісімдесят дві"},
            { 1,    "одна" },
            { 2,    "дві"},
            { 0,    "нуль"},
            { 219,  "двісті дев’ятнадцять"},
            { 10,   "десять"},
            { 18,   "вісімнадцять"},
            { 400,  "чотириста"},
            { 1000000000,  "один мільярд"},
            { 2000000,  "два мільйона"},
            { 7000000,  "сім мільйонів"},
            { 1782529, "один мільйон сімсот вісімдесят дві тисячі п’ятсот двадцять дев’ять"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.Spell(item.Key, culture, SpellGender.Female));
        }
    }


    [TestMethod]
    public void SimpleFemaleUA()
    {
        var culture = CultureInfo.CreateSpecificCulture("uk-UA");

        var dict = new Dictionary<Decimal, String>()
        {
            { 142,  "сто сорок дві"},
            { 1_002_001,  "один мільйон дві тисячі одна"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.Spell(item.Key, culture, SpellGender.Female));
        }
    }

    [TestMethod]
    public void SimpleMaleUA()
    {
        var culture = CultureInfo.CreateSpecificCulture("uk-UA");

        var dict = new Dictionary<Decimal, String>()
        {
            { 1,  "один"},
            { 142,  "сто сорок два"},
            { 1_002_001,  "один мільйон дві тисячі один"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.Spell(item.Key, culture, SpellGender.Male));
        }
    }

    [TestMethod]
    public void SimpleNeutralUA()
    {
        var culture = CultureInfo.CreateSpecificCulture("uk-UA");

        var dict = new Dictionary<Decimal, String>()
        {
            { 1,  "одне"},
            { 142,  "сто сорок два"},
            { 1_002_001,  "один мільйон дві тисячі одне"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.Spell(item.Key, culture, SpellGender.Neutral));
        }
    }

    [TestMethod]
    public void SimpleEN()
    {
        var dict = new Dictionary<Decimal, String>()
        {
            { 152,  "one hundred fifty-two" },
            { 1000, "one thousand" },
            { 1782, "one thousand seven hundred eighty-two"},
            { 1,    "one" },
            { 2,    "two"},
            { 0,    "zero"},
            { 219,  "two hundred nineteen"},
            { 10,   "ten"},
            { 18,   "eighteen"},
            { 400,  "four hundred"},
            { 3437,  "three thousand four hundred thirty-seven"},
            { 83432,  "eighty-three thousand four hundred thirty-two"},
            { 1000000000,  "one billion"},
            { 1231200000002,  "one trillion two hundred thirty-one billion two hundred million two"},
            { 2000000,  "two million"},
            { 7000000,  "seven million"},
            { 1782529, "one million seven hundred eighty-two thousand five hundred twenty-nine"},
        };

        foreach (var item in dict)
        {
            Assert.AreEqual(item.Value, SpellString.SpellEn(item.Key));
        }
    }
}