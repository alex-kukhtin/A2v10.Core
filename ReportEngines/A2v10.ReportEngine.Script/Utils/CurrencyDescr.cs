// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Globalization;

namespace A2v10.ReportEngine.Script;

internal abstract class CurrencyDescr
{
#pragma warning disable IDE1006 // Naming Styles
	protected abstract CurrencyDef _descr { get; }
#pragma warning restore IDE1006 // Naming Styles

	public String NameCeil(SpellType unit)
	{
		return _descr.Ceils[(Int32) unit];
	}

	public String NameFract(SpellType unit)
	{
		return _descr.Fracts[(Int32) unit];
	}

	public SpellGender CeilGender => _descr.CeilGender;
	public SpellGender FractGender => _descr.FractGender;

	public static CurrencyDescr FromCulture(CultureInfo culture, String currencyCode)
	{
		return culture.TwoLetterISOLanguageName switch
		{
			"uk" => new CurrencyDescrUA(currencyCode),
			"en" => new CurrencyDescrEN(currencyCode),
			_ => throw new InvalidOperationException($"Spell for '{culture.Name}' yet not supported")
		};
	}
}

internal readonly struct CurrencyDef
{
	public String[] Ceils { get; init; }
	public String[] Fracts { get; init; }
	public SpellGender CeilGender { get; init; }
	public SpellGender FractGender { get; init; }
}

internal class CurrencyDescrUA(String currencyCode) : CurrencyDescr
{
	readonly CurrencyDef _current = currencyCode switch
    {
        "980" => _uah_ua,
        "978" => _uah_eur,
        "840" => _uah_usd,
        _ => throw new ArgumentOutOfRangeException($"Currency descriptor for '{currencyCode}' yet not implemented")
    };
	private static readonly CurrencyDef _uah_ua = new()
	{
		Ceils = "гривень|гривня|гривні".Split('|'),
		CeilGender = SpellGender.Female,
		Fracts = "копійок|копійка|копійки".Split('|'),
		FractGender = SpellGender.Female
	};

	private static readonly CurrencyDef _uah_usd = new()
	{
		Ceils = "доларів|долар|долари".Split('|'),
		CeilGender = SpellGender.Male,
		Fracts = "центів|цент|цента".Split('|'),
		FractGender = SpellGender.Male
	};

	private static readonly CurrencyDef _uah_eur = new()
	{
		Ceils = "євро|євро|євро".Split('|'),
		CeilGender = SpellGender.Male,
		Fracts = "центів|цент|цента".Split('|'),
		FractGender = SpellGender.Male
	};

    protected override CurrencyDef _descr => _current;
}

internal class CurrencyDescrEN(String currencyCode) : CurrencyDescr
{
    private readonly CurrencyDef _current = currencyCode switch
    {
        "980" => _uah_ua,
        "978" => _uah_eur,
        "840" => _uah_usd,
        _ => throw new ArgumentOutOfRangeException($"Currency descriptor for '{currencyCode}' yet not implemented")
    };
    private readonly static CurrencyDef _uah_ua = new()
    {
        Ceils = "hryvnias|hryvnia|".Split('|'),
        CeilGender = SpellGender.Female,
        Fracts = "kopecks|kopeck|".Split('|'),
        FractGender = SpellGender.Female
    };

    private readonly static CurrencyDef _uah_usd = new()
    {
        Ceils = "dollars|dollar|".Split('|'),
        CeilGender = SpellGender.Male,
        Fracts = "cents|cent|".Split('|'),
        FractGender = SpellGender.Male
    };

    private readonly static CurrencyDef _uah_eur = new()
    {
        Ceils = "euro|euro|".Split('|'),
        CeilGender = SpellGender.Male,
        Fracts = "cents|cent|".Split('|'),
        FractGender = SpellGender.Male
    };

    protected override CurrencyDef _descr => _current;
}

