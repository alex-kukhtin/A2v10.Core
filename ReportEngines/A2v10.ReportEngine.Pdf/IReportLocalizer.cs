// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Globalization;

namespace A2v10.ReportEngine.Pdf;

public interface IReportLocalizer
{
	String? Localize(String? content);
	CultureInfo CurrentCulture { get; }
}
