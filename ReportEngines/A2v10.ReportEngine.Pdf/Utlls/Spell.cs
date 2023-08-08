// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;

using System.Globalization;
using System.Text;

namespace A2v10.ReportEngine.Pdf;

public enum SpellGender
{
	Male,
	Female,
	Neutral
}

public enum SpellType
{
	zero = 0,
	one = 1,
	two = 2
}


public static class SpellString
{
	static readonly SpellType[] _intTypes = new SpellType[5] 
	{
		SpellType.zero ,SpellType.one, SpellType.two, SpellType.two, SpellType.two,
	};


	public static String Spell(Decimal val, CultureInfo culture, SpellGender gender)
	{
		var nums = LangNumbers.FromCulture(culture);
		var strPresentation = val.ToString("F2", CultureInfo.InvariantCulture);
		var vals = strPresentation.Split('.');
		String intPart = strPresentation;
		if (vals.Length == 2)
		{
			intPart = vals[0];
		}
		return Int_spellNumber(intPart, nums, gender, out SpellType _);
	}

    public static String SpellEn(Decimal val)
    {
        var nums = new LangNumbersEN();
        var strPresentation = val.ToString("F2", CultureInfo.InvariantCulture);
        var vals = strPresentation.Split('.');
        String intPart = strPresentation;
        //String fractPart = String.Empty;
        if (vals.Length == 2)
        {
            intPart = vals[0];
            //fractPart = vals[1];
        }
        return SpellNumberIntEn(intPart, nums);
    }


    public static String SpellCurrency(Decimal val, CultureInfo culture, String currencyCode)
	{
		var nums = LangNumbers.FromCulture(culture);
		var strPresentation = val.ToString("F2", CultureInfo.InvariantCulture);
		var vals = strPresentation.Split('.');
		String intPart = strPresentation;
		String fractPart = String.Empty;
		if (vals.Length == 2)
		{
			intPart = vals[0];
			fractPart = vals[1];
		}

		var currencyDesc = CurrencyDescr.FromCulture(culture, currencyCode);

		var sb = new StringBuilder();

		sb.Append(Int_spellNumber(intPart, nums, currencyDesc.CeilGender, out SpellType type));
		sb.Append(' ');
		sb.Append(currencyDesc.NameCeil(type));
		sb.Append(' ');
		sb.Append(fractPart);
		sb.Append(' ');
		sb.Append(currencyDesc.NameFract(LastNumberFract(fractPart)));
		sb[0] = Char.ToUpper(sb[0]);
		return sb.ToString();

	}

    public static String SpellCurrencyEn(Decimal val, String currencyCode)
    {
        var nums = new LangNumbersEN();
        var strPresentation = val.ToString("F2", CultureInfo.InvariantCulture);
        var vals = strPresentation.Split('.');
        String intPart = strPresentation;
        String fractPart = String.Empty;
        if (vals.Length == 2)
        {
            intPart = vals[0];
            fractPart = vals[1];
        }

        var currencyDesc = new CurrencyDescrEN(currencyCode);

        var sb = new StringBuilder();

        sb.Append(SpellNumberIntEn(intPart, nums));
        sb.Append(' ');
        sb.Append(currencyDesc.NameCeil(intPart == "1" ? SpellType.one : SpellType.zero));
        sb.Append(" and ");
        sb.Append(fractPart);
        sb.Append(' ');
        sb.Append(currencyDesc.NameFract(fractPart == "01" ? SpellType.one : SpellType.zero));
        sb[0] = Char.ToUpper(sb[0]);
        return sb.ToString();

    }

    static SpellType LastNumberFract(String fract)
	{
		// 0,1,2
		if (String.IsNullOrEmpty(fract))
			return SpellType.zero;
		// xx[\0or или x[\0]
		Int32 dig;
		if (fract[1] == '0') {
			dig = (int)(fract[0] - '0');
		}  else { 
			dig = (int)(fract[0] - '0') * 10;
			dig += (int)(fract[1] - '0');
		}
		//0-> 0,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20
		//1-> 1,21,31,
		//2-> 2,3,4,22,23,24,32,33,34,...
		if (dig > 19)
		{
			// module
			dig %= 10;
		}
		if (dig == 2 || dig == 3 || dig == 4)
			return SpellType.two;
		else if (dig == 1)
			return SpellType.one;
		return SpellType.zero;
	}

	private static String Int_spellNumber(String number, LangNumbers numbers, SpellGender gender, out SpellType type)
	{
		type = SpellType.zero;
		if (String.IsNullOrEmpty(number) || number == "0" || number == "00")
			return numbers.Null(SpellGender.Male).Trim();

		Int32 len = number.Length;

		StringBuilder sb = new();

		var cha = number.ToCharArray();
		Array.Reverse(cha);
		number = new String(cha);

		Int32 k = len / 3;
		switch (len % 3)
		{
			case 0:
				k--;
				break;
			case 1:
				number += "00";
				break;
			case 2:
				number += "0";
				break;
		}

		for (int i = k; i >= 0; i--)
		{
			var trigraph = number.Substring(i * 3, 3);
			if (trigraph == "000")
				continue;
			int hundred = trigraph[2] - '0';
			int ten = trigraph[1] - '0';
			int unit = trigraph[0] - '0';

			sb.Append(numbers.Hundred(hundred));
			if (ten >= 2)
				sb.Append(numbers.Ten(ten));
			else if (ten == 1)
				unit += 10;			
			if (unit < 5)
				type = _intTypes[unit];
			else
				type = SpellType.zero;
			if (i == 1)
			{
				// 1000
				type = 0; // 1000 (2000, 3000 еtc) UAH
				sb.Append(numbers.Unit(unit, SpellGender.Female));
			}
			else if (i > 1)
			{
				type = SpellType.zero;
				sb.Append(numbers.Unit(unit, SpellGender.Male));
			}
			else if (i == 0)
				sb.Append(numbers.Unit(unit, gender));

			switch (unit)
			{
				case 1:
					sb.Append(numbers.Name(0, i));
					break;
				case 2:
				case 3:
				case 4:
					sb.Append(numbers.Name(1, i));
					break;
				default:
					sb.Append(numbers.Name(2, i));
					break;
			}
		}
		// remove last space
		if (sb[^1] == ' ')
			sb.Remove(sb.Length - 1, 1);
		return sb.ToString().Trim();
	}
    private static String SpellNumberIntEn(String number, LangNumbers numbers)
    {
        if (String.IsNullOrEmpty(number) || number == "0" || number == "00")
            return numbers.Null(SpellGender.Male).Trim();

        Int32 len = number.Length;

        StringBuilder sb = new();

        var cha = number.ToCharArray();
        Array.Reverse(cha);
        number = new String(cha);

        Int32 k = len / 3;
        switch (len % 3)
        {
            case 0:
                k--;
                break;
            case 1:
                number += "00";
                break;
            case 2:
                number += "0";
                break;
        }

        for (int i = k; i >= 0; i--)
        {
            var trigraph = number.Substring(i * 3, 3);
            if (trigraph == "000")
                continue;
            int hundred = trigraph[2] - '0';
            int ten = (trigraph[1] - '0');
            int tenAndUnit = (trigraph[1] - '0') * 10 + (trigraph[0] - '0');
            int unit = trigraph[0] - '0';

            if (hundred > 0)
            {
                sb.Append(numbers.Unit(hundred));
                sb.Append("hundred ");
            }
            if (tenAndUnit > 0)
            {
                if (tenAndUnit > 20)
                {
                    if (unit > 0)
                    {
                        sb.Append(numbers.Ten(ten).TrimEnd());
                        sb.Append('-');
                        sb.Append(numbers.Unit(unit));
                    }
                    else
                        sb.Append(numbers.Ten(ten));
                }
                else
                {
                    sb.Append(numbers.Unit(tenAndUnit));
                }
            }
            sb.Append(numbers.Name(0, i));
        }
        // remove last space
        if (sb[^1] == ' ')
            sb.Remove(sb.Length - 1, 1);
        return sb.ToString().Trim();
    }
}

