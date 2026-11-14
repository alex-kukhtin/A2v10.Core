// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The report body: the subset of the surface that is offered to the user, plus the layout it is
 * laid out in. The query itself is not here - the client composes it inside this subset.
 *
 * Still the old shape: 'reportItems', a flat list with a G/F/D discriminator, which came from the
 * designer UI that no longer exists. The format it is meant to become - three lists (groups /
 * filters / data) plus defaults, where the key IS the discriminator - is described in the skill's
 * references/report.md and is a separate step: it changes the runtime, not just the type.
 */


public sealed record ReportDefaultMetadata
{
    public List<String> Groups { get; init; } = [];
    public List<String> Data { get; init; } = [];
}

public sealed record ReportMetadata
{
    /* No 'surface' here. It is read off DeclarationMetadata, with 'table' and 'storage', because
     * all three answer one question and the loader resolves them in one place.
     */

    // layout discriminator; today 'turnover' is the only one
    public String Type { get; init; } = default!;
    public List<String> Groups { get; init; } = [];
    public List<String> Filters { get; init; } = [];
    public List<String> Data { get; init; } = [];
    public ReportDefaultMetadata Defaults { get; init; } = new();

    // filters a run cannot go without: a trial balance across companies adds up and means nothing
    public List<String> Required { get; init; } = [];

    // what a filter starts on when the page opens; the form of an endpoint's, keyed by filter
    public Dictionary<String, InitialMetadata> InitialValues { get; init; } = [];

    /* Both name filters, so both resolve against 'filters' and the surface - at load, where a wrong
     * name names the file. 'query' is the url's and a filter already is one; 'policy' has no registry
     * yet, and unrefused would reach the database as an undeclared variable. A 'profile' slot is
     * checked with the record's (DatabaseMetadataProvider.CheckProfileInitialsAsync).
     */
    internal void Check(String file, TableMetadata surface)
    {
        foreach (var name in Required.Where(n => !Filters.Contains(n)))
            throw new InvalidOperationException(
                $"{file}: required: '{name}' is not in 'filters'. A required filter is one of those offered");
        foreach (var (name, initial) in InitialValues)
        {
            if (!Filters.Contains(name))
                throw new InvalidOperationException(
                    $"{file}: initialValues: '{name}' is not in 'filters'. A report starts its filters, nothing else");
            if (initial.Source is not (InitialSource.Sql or InitialSource.Literal or InitialSource.Context or InitialSource.Profile))
                throw new InvalidOperationException(
                    $"{file}: initialValues: '{name}' - a report filter starts on 'sql', 'literal', 'context' or 'profile', not '{initial.Source}'");
            var column = surface.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"{file}: initialValues: '{name}' not found in surface '{surface.Path}'");
            DeclarationBake.CheckInitial(name, initial, column, $"{file}: initialValues");
        }
    }
}
