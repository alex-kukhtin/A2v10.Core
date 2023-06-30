// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.


using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IClrInvokeTarget
{
	Task<Object> InvokeAsync(ExpandoObject args);
}
