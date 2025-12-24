// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder: MainModelBuilder
{
    private readonly TableMetadata? _baseTable;
    private readonly IPlatformUrl _platformUrl;
    private readonly IDbContext _dbContext;

    internal PlainModelBuilder(BaseModelBuilder _baseModelBuilder)
        : base(_baseModelBuilder)
    {
        _baseTable = _baseModelBuilder._baseTable;
        _platformUrl = _baseModelBuilder._platformUrl;
        _dbContext = _baseModelBuilder._dbContext;
    }
 }
