// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The rows of the operation registry. Declared by no file as rows - a document lists its operations,
 * or is one itself over a document storage - so the deploy walk (AllElementsMetadata) collects them
 * and hands them here; json never can. Sorted, because the fingerprint is taken from the text and
 * must not depend on how the file system is enumerated. The name is the localization key the screens
 * show the operation by (MetadataExtensions.OperationLabel).
 */
internal static class OperationRows
{
    internal static IReadOnlyList<SeedRow> Rows(IEnumerable<OperationMetadata> operations) =>
        [.. operations.OrderBy(o => o.Id, StringComparer.Ordinal).Select(o =>
            new SeedRow(o.Id, new Dictionary<String, String?>
            {
                [Constants.FieldNames.Name] = MetadataExtensions.OperationLabel(o.Id),
                [Constants.FieldNames.Document] = o.Document,
                [Constants.FieldNames.Path] = o.Path,
                [Constants.FieldNames.Order] = o.Order.ToString()
            }))];
}
