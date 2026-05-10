// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal static class DefaultFormBuilder
{
    public static FormMetadata CreateIndexForm(TableMetadata table)
    {
        IEnumerable<FormColumn> indexColums()
        {
            yield return new FormColumn() { Field = "Id", Header = "@[Id]"};
            yield return new FormColumn() { Field = "Name", Header = "@[Name]" };
        }

        return new FormMetadata()
        {
            Columns = [..indexColums()]
        };
    }
}
