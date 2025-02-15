// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Services.Interop;

public record ExcelFormatError
{
	public String? Message;
	public String? CellReference;
	public String? Value;
}
