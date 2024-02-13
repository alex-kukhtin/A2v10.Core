// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.ReportEngine.Excel;

public static class CellRefs
{
	public static String Index2Col(UInt32 index)
	{
		UInt32 q = index / 26;

		if (q > 0)
			return Index2Col(q - 1) + (Char)((UInt32)'A' + (index % 26));
		else
			return String.Empty + (Char)((UInt32)'A' + index);
	}

	public static (UInt32 row, UInt32 column) Parse(String refs)
	{
		UInt32 ci = 0;
		UInt32 ri = 0;
		refs = refs.ToUpper();
		for (Int32 ix = 0; ix < refs.Length; ix++)
		{
			if (refs[ix] >= 'A')
				ci = (ci * 26) + ((UInt32)refs[ix] - 64);
			else
			{
				ri = UInt32.Parse(refs[ix..]) - 1;
				break;
			}
		}
		return (ri, ci - 1);
	}
}

