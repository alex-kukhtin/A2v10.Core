// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Platform.Web
{
	public sealed class InvalidRequestException(String message) : Exception(message)
	{
    }
}
