// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public interface IEmailTextProvider
{
	String? GetEmailBodyAsync(String key);
}
