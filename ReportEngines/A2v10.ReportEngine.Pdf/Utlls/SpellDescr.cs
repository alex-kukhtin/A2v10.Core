// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Globalization;

namespace A2v10.ReportEngine.Pdf;

internal abstract class LangNumbers
{
#pragma warning disable IDE1006 // Naming Styles
	protected abstract String[] _hundred { get; }
	protected abstract String[] _ten { get; }
	protected abstract String[] _unit { get; }
	protected abstract String[] _name { get; }
	protected abstract String[] _unitMale { get; }
	protected abstract String[] _unitFemale { get; }
	protected abstract String[] _unitNeutral { get; }
#pragma warning restore IDE1006 // Naming Styles

	public LangNumbers()
	{
		if (_hundred.Length != 10)
			throw new InvalidOperationException(nameof(_hundred));
		if (_ten.Length != 10)
			throw new InvalidOperationException(nameof(_ten));
		if (_unit.Length != 20)
			throw new InvalidOperationException(nameof(_unit));
		if (_name.Length != 15)
			throw new InvalidOperationException(nameof(_name));
		if (_unitMale.Length != 3)
			throw new InvalidOperationException(nameof(_unitMale));
		if (_unitFemale.Length != 3)
			throw new InvalidOperationException(nameof(_unitFemale));
		if (_unitNeutral.Length != 3)
			throw new InvalidOperationException(nameof(_unitNeutral));
	}
	public String Hundred(Int32 index)
	{
		if (index < 0 || index >= _hundred.Length)
			throw new ArgumentOutOfRangeException(nameof(index), nameof(Hundred));
		return _hundred[index];
	}
	public String Ten(Int32 index)
	{
		if (index < 0 || index >= _ten.Length)
			throw new ArgumentOutOfRangeException(nameof(index), nameof(Ten));
		return _ten[index];
	}

	public String Null(SpellGender _1/*gender*/)
	{
		return _unit[0];
	}

	public String Unit(Int32 index, SpellGender gender = SpellGender.Neutral)
	{
		if (index < 0 || index >= _unit.Length)
			throw new ArgumentOutOfRangeException(nameof(index));
		if (index < 3)
			return UnitGender(gender, index);
		return _unit[index];
	}

	public String Name(Int32 scale, Int32 index)
	{
		Int32 ix = index + scale * 5;
		if (ix < 0 || ix >= _name.Length)
			throw new ArgumentOutOfRangeException(nameof(scale));
		return _name[ix];
	}

	private String UnitGender(SpellGender gender, Int32 index)
	{
		return gender switch
		{
			SpellGender.Female => _unitFemale[index],
			SpellGender.Male => _unitMale[index],
			SpellGender.Neutral => _unitNeutral[index],
			_ => throw new ArgumentOutOfRangeException(nameof(gender))
		};
	}

	public static LangNumbers FromCulture(CultureInfo culture)
	{
		return culture.TwoLetterISOLanguageName switch
		{
			"uk" => new LangNumbersUA(),
			"en" => new LangNumbersEN(),
			 _ => throw new InvalidOperationException($"Spell for '{culture.Name}' yet not supported")
		};
	}
}

internal class LangNumbersUA : LangNumbers
{
	private static readonly String[] _hundredUa = ",сто ,двісті ,триста ,чотириста ,п’ятсот ,шістсот ,сімсот ,вісімсот ,дев’ятсот ".Split(',');
	private static readonly String[] _tenUa = ",,двадцять ,тридцять ,сорок ,п’ятдесят ,шістдесят ,сімдесят ,вісімдесят ,дев’яносто ".Split(',');
	private static readonly String[] _unitUa = "нуль ,один ,два ,три ,чотири ,п’ять ,шість ,сім ,вісім ,дев’ять ,десять ,одинадцять ,двaнадцять ,тринадцять ,чотирнадцять ,п’ятнадцять ,шістнадцять ,сімнадцять ,вісімнадцять ,дев’ятнадцять ".Split(',');
	private static readonly String[] _nameUa = ",тисяча ,мільйон ,мільярд ,трильйон ,,тисячі ,мільйона ,мільярда ,трильйона ,,тисяч ,мільйонів ,мільярдів ,трильйонів ".Split(',');
	private static readonly String[] _unitFemaleUa = ",одна ,дві ".Split(',');
	private static readonly String[] _unitNeutralUa = ",одне ,два ".Split(',');
	private static readonly String[] _unitMaleUa = ",один ,два ".Split(',');
	protected override String[] _hundred => _hundredUa;
	protected override String[] _ten => _tenUa;
	protected override String[] _unit => _unitUa;
	protected override String[] _name => _nameUa;
	protected override String[] _unitFemale => _unitFemaleUa;
	protected override String[] _unitMale => _unitMaleUa;
	protected override String[] _unitNeutral => _unitNeutralUa;
}

internal class LangNumbersEN : LangNumbers
{
    private static readonly String[] _hundredEn = ",,,,,,,,,".Split(',');
    private static readonly String[] _tenEn = ",,twenty ,thirty ,forty ,fifty ,sixty ,seventy ,eighty ,ninety ".Split(',');
    private static readonly String[] _unitEn = "zero ,one ,two ,three ,four ,five ,six ,seven ,eight ,nine ,ten ,eleven ,twelve ,thirteen ,fourteen ,fifteen ,sixteen ,seventeen ,eighteen ,nineteen ".Split(',');
    private static readonly String[] _nameEn = ",thousand ,million ,billion ,trillion ,,,,,,,,,,".Split(',');
    private static readonly String[] _unitFemaleEn = ",,".Split(',');
    private static readonly String[] _unitNeutralEn = ",one ,two ".Split(',');
    private static readonly String[] _unitMaleEn = ",,".Split(',');
    protected override String[] _hundred => _hundredEn;
    protected override String[] _ten => _tenEn;
    protected override String[] _unit => _unitEn;
    protected override String[] _name => _nameEn;
    protected override String[] _unitFemale => _unitFemaleEn;
    protected override String[] _unitMale => _unitMaleEn;
    protected override String[] _unitNeutral => _unitNeutralEn;
}


