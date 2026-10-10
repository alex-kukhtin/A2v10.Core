// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The rows a file declares WITH its table - one mechanism for every registry: a set's values, the
 * numberings, a chart's seed, the operation codes (OperationRows, filled by the deploy walk). Each
 * keeps its own grammar and its own checks in a file beside this one; what leaves is one thing,
 * TableMetadata.Rows - the key and the columns the row names - read by the fingerprint (Xtra) and
 * by the one merge the deploy writes (SqlDbGenerator.CreateRowsScript). See CLAUDE.md, "Rows
 * declared with the table".
 *
 * The checks are asked of EVERY file and not only of the kind that reads the key: 'values',
 * 'autonums' or 'seed' written where nothing reads them is refused rather than dropped, which is
 * the one failure of this format that leaves no trace at all.
 */
internal static class RegistryRows
{
    internal static async Task LoadAsync(TableMetadata storage, String schema, String table, IAppCodeProvider codeProvider)
    {
        var file = DatabaseMetadataProvider.MetadataFileName(schema, table);
        SetRows.Check(storage, file);
        AutonumRows.Check(storage, file);
        AccountRows.Check(storage, file);
        storage.Rows = storage.Kind switch
        {
            TableKind.Enum or TableKind.State => SetRows.Rows(storage),
            TableKind.Autonum => AutonumRows.Rows(storage),
            TableKind.AccPlan => await AccountRows.LoadAsync(storage, schema, table, codeProvider),
            _ => []
        };
        CheckKeys(storage, file);
    }

    // a key lands in the key column whatever grammar wrote it; one check for the four of them
    internal static void CheckKeys(TableMetadata storage, String file)
    {
        if (storage.Rows.Count == 0)
            return;
        var keyLength = storage.KeyColumn.DeployLength();
        foreach (var row in storage.Rows)
            if (row.Id.Length > keyLength)
                throw new InvalidOperationException($"{file}: '{row.Id}' - a key is at most {keyLength} characters");
    }
}
