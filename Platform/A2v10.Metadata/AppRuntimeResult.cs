// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public record AppRuntimeResult(IDataModel DataModel, String? ActionResult) : IAppRuntimeResult;
