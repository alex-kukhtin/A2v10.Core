// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace A2v10.Xaml.Report.Spreadsheet;

public class PageFooter
{
	public String? Left {  get; set; }
	public String? Center { get; set; }
	public String? Right { get; set; }

	public static PageFooter? FromString(String? source)
	{
		if (String.IsNullOrEmpty(source))
			return null;
		var pf = new PageFooter();
		//var leftPos = source.IndexOf("&L");
		var centerPos = source.IndexOf("&C");
		var rightPos = source.IndexOf("&R");
		if (centerPos >= 0)
		{
			if (rightPos < 0)
				rightPos = source.Length;
			pf.Center = ResolveExcelMacros(source.Substring(centerPos + 2, rightPos - centerPos - 2));
		}
		return pf;
	}

	public static String? ResolveExcelMacros(String? str)
	{
		if (String.IsNullOrEmpty(str))
			return null;
		return str.Replace("&P", "&(Page)").Replace("&N", "&(Pages)");
	}

	public static IEnumerable<String> Resolve(String source)
	{
		var pattern = "\\&\\((.+?)\\)";
		var ms = Regex.Matches(source, pattern);
		var ix = 0;
		foreach (var m in ms.Cast<Match>())
		{
			var head = source[ix..m.Index];
			if (!String.IsNullOrEmpty(head))
				yield return head;
			yield return m.Value;
			ix = m.Index + m.Length;
		}
	}
}
