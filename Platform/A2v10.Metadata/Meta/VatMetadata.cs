// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The 'vat' rule: the author names the fields, the rule computes Sum, Vat and Total of a row from
 * them. The roles are data, and everything generic - the schema, the resolve, the checks - is read
 * off this table; the formula is the generator's. A role carries no scope: its path says where the
 * field is. See a2v10-md-skill, concepts/vat.
 */
internal sealed record VatRole(String Name, Boolean IsOutput, ColumnType Domain, Boolean IsRequired);

internal static class VatRoles
{
    internal const String Qty = "Qty";
    internal const String Price = "Price";
    internal const String Rate = "Rate";
    internal const String Sum = "Sum";
    internal const String Vat = "Vat";
    internal const String Total = "Total";
    internal const String IsVat = "IsVat";
    internal const String Included = "Included";
    internal const String Manual = "Manual";

    /* An absent input is a constant, not a name looked up: no IsVat - VAT is charged, no Included -
     * the price is net, no Manual - nothing is typed by hand. Qty and Price come as a pair (a service
     * has neither); that is checked apart, a flag here would not say 'together'.
     */
    internal static readonly IReadOnlyList<VatRole> All =
    [
        new(Qty, false, ColumnType.Qty, false),
        new(Price, false, ColumnType.Price, false),
        new(Rate, false, ColumnType.Percent, true),
        new(Sum, true, ColumnType.Amount, true),
        new(Vat, true, ColumnType.Amount, true),
        new(Total, true, ColumnType.Amount, true),
        new(IsVat, false, ColumnType.Boolean, false),
        new(Included, false, ColumnType.Boolean, false),
        new(Manual, false, ColumnType.Boolean, false)
    ];

    internal static VatRole Of(String name) => All.First(r => r.Name == name);
}

/* One role resolved. 'Column' is where the path starts - in the row, or in the root under '$root';
 * 'RefField' is the column of the target read through it, kept as a name: the target is linked
 * after the bake, and phase 2 checks it (DatabaseMetadataProvider.CheckVatHops), as
 * InheritDescriptor.Source is kept.
 */
public sealed record VatOperand(TableColumn Column, Boolean IsRoot, String? RefField);

public sealed record VatDescriptor(IReadOnlyDictionary<String, VatOperand> Roles);
