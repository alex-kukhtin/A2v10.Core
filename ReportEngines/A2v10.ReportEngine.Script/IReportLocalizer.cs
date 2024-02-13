// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Globalization;

namespace A2v10.ReportEngine.Script;

public interface IReportLocalizer
{
	String? Localize(String? content);
	CultureInfo CurrentCulture { get; }
}
