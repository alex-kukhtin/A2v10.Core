// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata.Cli;

public class CliDeployDatabase(DatabaseMetadataProvider _metadataProvider, IAppCodeProvider _codeProvider,
    IDbContext _dbContext, CliDatabaseCreator _dbCreator)
{
    public async Task CreateTableType(String schema, String table)
    {
        var tableMeta = await _metadataProvider.GetSchemaAsync(null, schema, table);
        var sqlString = _dbCreator.CreateTableType(tableMeta);
        _dbContext.LoadModelSql(null, sqlString);
    }
}
