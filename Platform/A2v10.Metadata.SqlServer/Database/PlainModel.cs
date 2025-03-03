// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Metadata.SqlServer;

internal partial class DatabaseModelProcessor
{
    public Task<IDataModel> LoadPlainModelAsync(TableMetadata meta)
    {
        throw new NotImplementedException("Load Plain Model Async");
    }
}
