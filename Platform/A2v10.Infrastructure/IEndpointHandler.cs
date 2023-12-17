// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

namespace A2v10.Infrastructure;

public interface IEndpointHandler
{
    String RenderResult(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms);
}
