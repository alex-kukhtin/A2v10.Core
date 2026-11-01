// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The rules as code, for every edit template: a rule belongs to a collection, and a catalog has
 * collections as a document does (a price list computes VAT too). The document template and the
 * generic one take the same handlers and functions from here.
 */
internal partial class ScriptBuilder
{
    private IEnumerable<RowSetDeclaration> RowSets =>
        Endpoint.Declaration.Details.Values.SelectMany(d => d.RowSets);

    // a kind is its own type (EditTSMap), so what is imported is the row sets' and not the collections'
    private IEnumerable<String> TemplateTypes()
    {
        yield return "TRoot";
        yield return Table.TypeName;
        foreach (var rs in RowSets)
        {
            yield return rs.Type;
            yield return $"{rs.Type}Array";
        }
    }

    /* One handler per PATH. An inherit off a reference and a vat reading through it are one body,
     * inherit first - it writes what vat then reads; two equal keys of the literal TS refuses. A
     * header path has the record as its argument, a row path the row; both keep the order in which
     * they were first named.
     */
    private IEnumerable<String> RuleEvents()
    {
        List<(String Path, String Arg, String Type, List<String> Body)> handlers = [];

        void add(String path, String arg, String type, String statement)
        {
            var h = handlers.FirstOrDefault(x => x.Path == path);
            if (h.Path == null)
                handlers.Add((path, arg, type, [statement]));
            else if (!h.Body.Contains(statement))
                h.Body.Add(statement);
        }

        foreach (var (refName, inherits) in Endpoint.Declaration.Inherits)
            foreach (var x in inherits)
                add($"{Table.Model}.{refName}", "doc", Table.TypeName, $"doc.{x.Field.Name} = doc.{x.Ref.Name}.{x.Source};");

        // inherit lands on the PATH too - one handler per row set, not one per collection
        foreach (var rs in RowSets)
            foreach (var (refName, inherits) in rs.Inherits)
                foreach (var x in inherits)
                    add($"{Table.Model}.{rs.Collection}[].{refName}", "row", rs.Type, $"row.{x.Field.Name} = row.{x.Ref.Name}.{x.Source};");

        foreach (var rs in RowSets)
            if (rs.Vat is { } vat)
                foreach (var (role, operand) in vat.Roles)
                {
                    var typed = VatTyped(role);
                    if (role == VatRoles.Vat && !vat.Roles.ContainsKey(VatRoles.Manual))
                        continue;
                    if (operand.IsRoot)
                        add($"{Table.Model}.{operand.Column.ModelName}", "doc", Table.TypeName,
                            $"doc.{rs.Collection}.forEach(r => {VatFunction(rs)}(r, '{typed}'));");
                    else
                        add($"{Table.Model}.{rs.Collection}[].{operand.Column.ModelName}", "row", rs.Type,
                            $"{VatFunction(rs)}(row, '{typed}');");
                }

        foreach (var (path, arg, type, body) in handlers)
            yield return $$"""'{{path}}.change'({{arg}}{{Ann(type)}}) { {{String.Join(" ", body)}} }""";
    }

    /* What a change of a role asks the function to do: recompute from the primary values, or keep
     * the sum that was typed. Vat is typed only under Manual - without that role its column is shown
     * and never typed (MemberDescriptor.Rule), so it has no handler.
     */
    private static String VatTyped(String role) => role switch
    {
        VatRoles.Sum => "sum",
        VatRoles.Total => "total",
        VatRoles.Vat => "vat",
        _ => "calc"
    };

    private static String VatFunction(RowSetDeclaration rs) => $"vat{rs.Collection}";

    // 'utils' only where a function rounds with it
    private String RuleRequires() => RowSets.Any(rs => rs.Vat != null)
        ? $"const utils{Ann("Utils")} = require('std:utils');\n\n"
        : String.Empty;

    /* One function per row set with 'vat', the formula of concepts/vat, plan item 13 - with the
     * row's own fields named, so the compiler checks every one of them against the map. An absent
     * input is a constant: no IsVat - charged, no Included - net, no Manual - never by hand. Written
     * under $noChangeEvents: what it writes fires nothing back.
     *
     * Invariant Total = Sum + Vat, Manual included. 'calc' starts from Price * Qty, or - with no Qty
     * and Price - from the sum the price would be in; 'sum' and 'total' keep what was typed, and
     * outside Manual write the price back. 'vat' exists under Manual only, and keeps the sum the
     * price is in.
     */
    private IEnumerable<String> RuleFunctions()
    {
        foreach (var rs in RowSets)
        {
            if (rs.Vat is not { } vat)
                continue;

            String path(VatOperand op) =>
                $"{(op.IsRoot ? "doc" : "row")}.{op.Column.ModelName}{(op.RefField != null ? $".{op.RefField}" : "")}";
            String? input(String role) => vat.Roles.TryGetValue(role, out var op) ? path(op) : null;

            var hasDoc = vat.Roles.Values.Any(op => op.IsRoot);
            var isVat = input(VatRoles.IsVat);
            var included = input(VatRoles.Included) ?? "false";
            var manual = input(VatRoles.Manual);
            var qty = input(VatRoles.Qty);
            var price = input(VatRoles.Price);
            var rate = $"({input(VatRoles.Rate)} || 0) / 100";
            var sum = input(VatRoles.Sum)!;
            var vatCol = input(VatRoles.Vat)!;
            var total = input(VatRoles.Total)!;

            var calc = qty != null
                ? $"r2({price} * {qty})"
                : $"included ? {total} : {sum}";
            var priceBack = qty != null
                ? $$"""

                		if (typed !== 'calc' && {{qty}})
                			{{price}} = utils.currency.round((included ? {{total}} : {{sum}}) / {{qty}}, 6);
                """
                : String.Empty;
            var manualBranch = manual != null
                ? $$"""

                		if ({{manual}}) {
                			if (typed === 'sum' || (typed === 'vat' && !included))
                				{{total}} = r2({{sum}} + {{vatCol}});
                			else if (typed === 'total' || typed === 'vat')
                				{{sum}} = r2({{total}} - {{vatCol}});
                			return;
                		}
                """
                : String.Empty;

            yield return $$"""
                function {{VatFunction(rs)}}(row{{Ann(rs.Type)}}, typed{{Ann("'calc' | 'sum' | 'total' | 'vat'")}}) {{{(hasDoc ? $"\n\tconst doc = row.$root.{Table.Model};" : "")}}
                	const r2 = (v{{Ann("number")}}) => utils.currency.round(v, 2);
                	const included = {{included}};
                	const k = {{(isVat != null ? $"{isVat} ? {rate} : 0" : rate)}};
                	row.$noChangeEvents(() => {{{manualBranch}}
                		const gross = typed === 'total' || (typed === 'calc' && included);
                		const amount = typed === 'sum' ? {{sum}} : typed === 'total' ? {{total}} : {{calc}};
                		{{vatCol}} = r2(gross ? amount * k / (1 + k) : amount * k);
                		{{sum}} = gross ? r2(amount - {{vatCol}}) : amount;
                		{{total}} = r2({{sum}} + {{vatCol}});{{priceBack}}
                	});
                }
                """;
        }
    }
}
