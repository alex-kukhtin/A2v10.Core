// Copyright © 2021 Alex Kukhtin. All rights reserved.

namespace A2v10.Web.Identity;
public class JwtToken
{
	public String? Token { get; set; }
	public Int64 UserId { get; set; }
	public DateTime Expires { get; set; }
}
