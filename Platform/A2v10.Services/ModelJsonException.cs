// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Services
{
	public sealed class ModelJsonException : Exception
	{
		public ModelJsonException(String msg)
			:base(msg)
		{
		}
	}
}
