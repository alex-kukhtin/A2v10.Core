// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Text;

using Newtonsoft.Json;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;
using A2v10.Services;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private async Task<IInvokeResult> FetchAsync(ExpandoObject? prms)
    {
        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        declare @fr nvarchar(255);
        set @fr = N'%' + @Text + N'%';

        select top(100) [{_table.RealItemsName}!{_table.RealTypeName}!Array] = null, 
            [{_table.PrimaryKeyField}!!Id] = a.[{_table.PrimaryKeyField}], [{_table.NameField}!!Name] = a.[{_table.NameField}]
        from {_table.SqlTableName} a
        where a.[{_table.VoidField}] = 0 and
            (a.[{_table.NameField}] like @fr)
        order by a.[{_table.NameField}];
        """;

        var model = await _dbContext.LoadModelSqlAsync(_dataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Text", prms?.Get<String>("Text"));
        });

        var strResult = model != null && model.Root != null ?
            JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings) : "{}";

        return new InvokeResult(
            body: strResult != null ? Encoding.UTF8.GetBytes(strResult) : [],
            contentType: MimeTypes.Application.Json
        );
    }
}
