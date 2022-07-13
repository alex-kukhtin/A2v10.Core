// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System;
using System.Globalization;

using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Pdf;

public class PdfReportLocalizer : IReportLocalizer
{
	private readonly ILocalizer _localizer;
	private readonly String _locale;
	public PdfReportLocalizer(String locale, ILocalizer localizer)
	{
		_localizer = localizer;
		_locale = locale;
		CurrentCulture = new CultureInfo(locale);
	}

	public CultureInfo CurrentCulture { get; }

	public String? Localize(String? content)
	{
		return _localizer.Localize(_locale, content);
	}
}
