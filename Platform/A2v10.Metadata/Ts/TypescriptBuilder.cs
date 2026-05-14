// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal partial class TypescriptBuilder(BuilderDescriptor desciptor)
{
    private readonly BuilderDescriptor _descr = desciptor;
    private readonly TableMetadata Table = desciptor.Table;

    public IEnumerable<String> TsProperties(TableMetadata table)
    {
        static String property(TableColumn column)
        {
            var ro = column.IsFieldUpdated() ? "" : "readonly ";
            if (column.IsReference)
            {
                //var refMember = _refFields.FindRefMember(column);
                //if (refMember != null)
                    //return $"\t{ro}{column.Name}: {refMember.Table.TypeName};";
            }
            return $"\t{ro}{column.Name}: {column.Type.ToTsType()};";
        }

        foreach (var p in table.Columns.Where(c => !c.IsVoid && c.Type != ColumnType.RowVersion))
            yield return property(p);
    }
}
