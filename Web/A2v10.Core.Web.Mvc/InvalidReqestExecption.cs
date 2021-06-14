// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Core.Web.Mvc
{
	public sealed class InvalidReqestExecption : Exception
	{
		public InvalidReqestExecption(String message)
			:base(message)
		{

		}
	}
}
