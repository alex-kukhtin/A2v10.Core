
using System;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

public record AppRuntimeResult(IDataModel DataModel, String? ActionResult) : IAppRuntimeResult;
