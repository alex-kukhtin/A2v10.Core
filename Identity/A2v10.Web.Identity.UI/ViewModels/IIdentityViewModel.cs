// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.UI;
public interface IIdentityViewModel
{
	String? RequestToken { get; }
	AppTitleModel? Title { get; }

	String Theme { get; }
}

