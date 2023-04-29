// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Web.Identity.UI;

public class SimpleIdentityViewModel : IIdentityViewModel
{
	public String? RequestToken { get; set; }

	public AppTitleModel? Title { get; init; }
	public String Theme { get; init; } = String.Empty;
}
