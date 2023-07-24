// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Web.Identity;
public class JwtToken<T>
{
	public String? Token { get; set; }
	public T? UserId { get; set; }
	public DateTime Expires { get; set; }
}
