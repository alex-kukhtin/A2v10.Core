// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.UI;

public record SimpleIdentityViewModel : IIdentityViewModel
{
	public String? RequestToken { get; set; }

	public AppTitleModel? Title { get; init; }
	public String Theme { get; init; } = String.Empty;
}
