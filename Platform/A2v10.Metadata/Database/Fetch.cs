// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;
using System.Dynamic;
using DocumentFormat.OpenXml.EMMA;
using Newtonsoft.Json;
using A2v10.Services;
using System.Text;

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

        select top(100) [{_table.RealItemsName}!{_table.RealTypeName}!Array] = null, [{_appMeta.IdField}!!Id] = a.[{_appMeta.IdField}], [{_appMeta.NameField}!!Name] = a.[{_appMeta.NameField}]
        from {_table.SqlTableName()} a
        where a.[{_appMeta.VoidField}] = 0 and
            (a.[{_appMeta.NameField}] like @fr)
        order by a.[{_appMeta.NameField}];
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
