// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Services.Interop.ExportTo
{
	public sealed class ExportToExcelException : Exception
	{
		public ExportToExcelException(String message)
			: base(message)
		{
		}
	}
}
