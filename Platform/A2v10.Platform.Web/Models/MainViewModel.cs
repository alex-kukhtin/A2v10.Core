// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Platform.Web;
public class MainViewModel
{
	public String? PersonName { get; init; }
	public Boolean Debug { get; init; }
	public String? HelpUrl { get; init; }
	public String? ModelStyles { get; init; }
	public String? ModelScripts { get; init; }
	public Boolean HasNavPane { get; init; }
	public Boolean HasProfile { get; init; }
	public Boolean HasSettings { get; init; }
	public String Theme { get; init; } = String.Empty;
	public String Minify { get; init; } = String.Empty;
	public String? SinglePagePath { get; set; }
}

