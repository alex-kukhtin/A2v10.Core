// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public interface IApplicationTheme
{
	String MakeTheme();
	String LogoUrl();

	String BodyCssClass { get; }

	Boolean IsDarkThemeEnabled { get; }
    Boolean IsDark { get; }
}
