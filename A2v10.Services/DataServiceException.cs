// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Services
{
	public sealed class DataServiceException : Exception
	{
		public DataServiceException(String msg)
			:base(msg)
		{
		}
	}
}
