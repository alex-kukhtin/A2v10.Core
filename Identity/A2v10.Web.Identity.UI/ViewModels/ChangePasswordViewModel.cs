// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Web.Identity.UI;

public class ChangePasswordViewModel
{
	public String? OldPassword { get; set; }
	public String? NewPassword { get; set; }
	public String? RequestToken { get; set; }
}
