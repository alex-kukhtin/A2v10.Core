// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.UI;

public record LoginViewModel : SimpleIdentityViewModel
{
	public String? Login { get; set; }
	public String? Password { get; set; }
	public Boolean RememberMe { get; set; }
	public String? LoginProviders { get; set; }
	public Boolean IsPersistent => RememberMe;
	public String? ReturnUrl { get; init; }
}

public record RegisterViewModel : SimpleIdentityViewModel
{
	public String? Login { get; set; }
	public String? PersonName { get; set; }
	public String? Password { get; set; }
	public String? Phone { get; set; }
}

public record InviteViewModel : SimpleIdentityViewModel
{
    public String? Login { get; set; }
    public String? PersonName { get; set; }
    public String? Password { get; set; }
    public String? Phone { get; set; }
	public String? Token { get; set;}
}

public record ConfirmCodeViewModel : SimpleIdentityViewModel
{
	public String Code { get; init; } = String.Empty;
	public String? Email { get; set; } = String.Empty;
	public String? Token { get; set;}
}

public record ForgotPasswordViewModel : SimpleIdentityViewModel
{
    public String? Login { get; set; }
}

public record ForgotPasswordCodeViewModel : SimpleIdentityViewModel
{
    public String? Login { get; set; }
    public String Code { get; init; } = String.Empty;
}

public record ForgotPasswordChangeViewModel : SimpleIdentityViewModel
{
    public String? Login { get; set; }
    public String Code { get; init; } = String.Empty;
    public String Password { get; init; } = String.Empty;
}

public record InitPasswordModel(Int64 UserId, String Token, DateTime Time);


public record InitPasswordViewModel : SimpleIdentityViewModel
{
	public String? Login { get; set; }
	public String Token { get; init; } = String.Empty;
	public String Password { get; init; } = String.Empty;
}

public record TwoFactorViewModel : SimpleIdentityViewModel
{
	public String Login { get; set; } = String.Empty;
	public String Code { get; init; } = String.Empty;
	public Boolean RememberMe { get; set; }
	public Boolean IsPersistent => RememberMe;
	public String? ReturnUrl { get; init; }
}
