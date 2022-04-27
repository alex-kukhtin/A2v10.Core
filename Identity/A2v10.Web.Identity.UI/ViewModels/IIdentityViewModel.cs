// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Web.Identity.UI;
public interface IIdentityViewModel
{
	String? RequestToken { get; }
	AppTitleModel? Title { get; }

	String Theme { get; }
}

