// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Services.Interop;

internal record ExcelFormatError
{
	public String? Message;
	public String? CellReference;
	public String? Value;
}
