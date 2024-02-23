// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IEndpointHandler
{
    Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms);
}
