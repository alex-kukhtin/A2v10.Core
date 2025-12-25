// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Globalization;
using System.Text;

using A2v10.Data.Core.Extensions;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal class MainModelBuilder(BaseModelBuilder _baseModelBuilder)
{
    protected readonly TableMetadata _table = _baseModelBuilder._table;
    protected readonly ICurrentUser _currentUser = _baseModelBuilder._currentUser;
    protected readonly DatabaseMetadataProvider _metadataProvider = _baseModelBuilder._metadataProvider;
    protected readonly String? _dataSource = _baseModelBuilder._dataSource;
    protected readonly AppMetadata _appMeta = _baseModelBuilder._appMeta;
    protected readonly IEnumerable<ReferenceMember> _refFields = _baseModelBuilder._refFields;

    protected DbParameterCollection AddDefaultParameters(DbParameterCollection prms)
    {
        if (_currentUser.Identity.Tenant != null)
            prms.AddInt("@TenantId", _currentUser.Identity.Tenant);
        prms.AddBigInt("@UserId", _currentUser.Identity.Id);
        return prms;
    }

    protected DbParameterCollection AddPeriodParameters(DbParameterCollection prms, ExpandoObject? qry)
    {
        if (!_table.HasPeriod())
            return prms;

        static DateTime? DateTimeFromString(String? value)
        {
            if (value == null)
                return null;
            return DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture);
        }

        return prms.AddDate("@From", DateTimeFromString(qry?.Get<String>("From")))
            .AddDate("@To", DateTimeFromString(qry?.Get<String>("To")));
    }

    protected static String RefTableJoins(IEnumerable<ReferenceMember> refFields, String alias)
    {
        return String.Join("\n", refFields.Select(refField =>
        {
            return $"    left join {refField.Table.SqlTableName} r{refField.Index} on {alias}.[{refField.Column.Name}] = r{refField.Index}.[{refField.Table.PrimaryKeyField}]";
        }));
    }

    protected static String EnumsMapSql(IEnumerable<ReferenceMember> enums, Boolean isFilter)
    {
        var sb = new StringBuilder();
        var where = isFilter ? "" : " where e.[Id] <> N''";
        foreach (var r in enums)
        {
            sb.AppendLine($"""
                select [{r.Table.RealItemsName}!TR{r.Table.RealItemName}!Map] = null, [Id!!Id] = e.Id, [Name!!Name] = e.[Name]
                from {r.Table.SqlTableName} e
                {where}
                order by e.[Order];
                """);
        }
        return sb.ToString();
    }

    public IEnumerable<String> TsProperties(TableMetadata table)
    {
        String property(TableColumn column)
        {
            var ro = column.IsFieldUpdated() ? "" : "readonly ";
            if (column.IsReference)
            {
                var refMember = _refFields.FindRefMember(column);
                if (refMember != null)
                    return $"\t{ro}{column.Name}: {refMember.Table.RealTypeName};";
            }
            return $"\t{ro}{column.Name}: {column.DataType.ToTsType(_appMeta.IdDataType)};";
        }

        foreach (var p in table.Columns.Where(c => !c.IsVoid && c.DataType != ColumnDataType.RowVersion))
            yield return property(p);
    }
}
