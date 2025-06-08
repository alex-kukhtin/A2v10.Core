// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder: MainModelBuilder
{
    private readonly TableMetadata? _baseTable;
    private readonly IPlatformUrl _platformUrl;
    private readonly IDbContext _dbContext;
    private readonly IEnumerable<ReferenceMember> _refFields;
    private readonly AppMetadata _appMeta;

    internal IndexModelBuilder(BaseModelBuilder _baseModelBuilder)
        : base(_baseModelBuilder)
    {
        _baseTable = _baseModelBuilder._baseTable;
        _platformUrl = _baseModelBuilder._platformUrl;
        _dbContext = _baseModelBuilder._dbContext;
        _refFields = _baseModelBuilder._refFields;
        _appMeta = _baseModelBuilder.AppMeta;
    }
 }
