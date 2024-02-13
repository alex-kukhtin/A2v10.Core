// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System;
using System.Globalization;

using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Script;

public class DefaultReportLocalizer(String locale, ILocalizer localizer) : IReportLocalizer
{
	private readonly ILocalizer _localizer = localizer;
	private readonly String _locale = locale;

    public CultureInfo CurrentCulture { get; } = new CultureInfo(locale);

    public String? Localize(String? content)
	{
		return _localizer.Localize(_locale, content);
	}
}
