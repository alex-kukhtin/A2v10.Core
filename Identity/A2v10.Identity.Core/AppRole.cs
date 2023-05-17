// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Web.Identity;

public class AppRole<T> where T : struct
{
	public T Id { get; set; }
	public String? Name { get; set; }
}
