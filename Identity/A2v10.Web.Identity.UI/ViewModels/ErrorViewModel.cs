// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.UI;

public record ErrorViewModel : SimpleIdentityViewModel
{
	public String? Message { get; set; }
}
