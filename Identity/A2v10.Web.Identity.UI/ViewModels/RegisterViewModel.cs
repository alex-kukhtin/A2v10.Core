// Copyright © 2021-2022 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Web.Identity.UI;

public class RegisterViewModel : IIdentityViewModel
{
	public String? Login { get; set; }
	public String? Password { get; set; }

	public String? RequestToken { get; set; }

	public AppTitleModel? Title { get; init; }
	public String Theme { get; init; } = String.Empty;
}
