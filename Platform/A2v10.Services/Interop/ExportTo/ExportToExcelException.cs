// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Services.Interop;

public sealed class ExportToExcelException : Exception
{
	public ExportToExcelException(String message)
		: base(message)
	{
	}
}
