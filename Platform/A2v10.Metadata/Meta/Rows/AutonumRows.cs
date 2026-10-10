// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The numberings ('autonums' of /autonum): refused where the file is read, because nothing
 * downstream can report it - the SQL that issues a number cannot parse a pattern it was handed; a
 * pattern with no counter in it yields a number, the same one for every document, and it gets saved.
 */
internal static class AutonumRows
{
    internal static void Check(TableMetadata storage, String file)
    {
        if (storage.Autonums.Count == 0)
            return;
        if (storage.Kind != TableKind.Autonum)
            throw new InvalidOperationException(
                $"{file}: 'autonums' declares numberings, and they are declared in autonum/metadata.json. Name one here with \"autonum\": \"<id>\" instead");

        foreach (var autonum in storage.Autonums)
        {
            if (String.IsNullOrEmpty(autonum.Id))
                throw new InvalidOperationException($"{file}: 'autonums' declares a numbering under an empty key");
            if (String.IsNullOrEmpty(autonum.Pattern))
                throw new InvalidOperationException(
                    $"{file}: '{autonum.Id}' declares no 'pattern'. The pattern IS the number: \"{{yyyy}}-{{nnnnn}}\"");
            CheckPattern(file, autonum);
        }
    }

    // The placeholders the procedure substitutes: the dates and the company's prefix. '{n}' through
    // '{nnnnn}' is the counter and is checked apart: its length is what it says, so it is not a name in a list.
    private static readonly String[] _placeholders = ["yy", "yyyy", "mm", "qq", "p"];

    /* Every '{...}' is read, because the procedure does not read them - it substitutes the ones it
     * knows and leaves the rest standing in the number. And the counter is checked twice: that it
     * is there at all, and that the pattern tells its periods apart. A monthly counter under a
     * pattern with no month issues '5' twice a year, which is the one failure here that produces a
     * plausible document rather than an error.
     */
    private static void CheckPattern(String file, AutonumMetadata autonum)
    {
        var pattern = autonum.Pattern;
        var head = $"{file}: pattern '{pattern}' of '{autonum.Id}'";
        var found = new List<String>();
        var counter = false;
        var ix = 0;
        while (ix < pattern.Length)
        {
            var start = pattern.IndexOf('{', ix);
            if (start < 0)
                break;
            var end = pattern.IndexOf('}', start);
            if (end < 0)
                throw new InvalidOperationException($"{head} has a '{{' that is never closed");
            var token = pattern[(start + 1)..end];
            if (token.Length > 0 && token.All(c => c == 'n'))
            {
                if (counter)
                    throw new InvalidOperationException(
                        $"{head} writes the counter twice; only the first would be filled in");
                counter = true;
            }
            else if (!_placeholders.Contains(token))
                throw new InvalidOperationException(
                    $"{head} uses '{{{token}}}', which is nothing. Known: {{yy}}, {{yyyy}}, {{mm}}, {{qq}}, {{p}} for the company's prefix, and {{n}} to {{nnnnn}} for the counter");
            found.Add(token);
            ix = end + 1;
        }
        if (!counter)
            throw new InvalidOperationException(
                $"{head} has no counter in it. Write it as '{{n}}', one 'n' per digit - '{{nnnnn}}' pads to five");

        var year = found.Contains("yy") || found.Contains("yyyy");
        var missing = autonum.Period switch
        {
            AutonumPeriod.Year => year ? null : "{yyyy}",
            AutonumPeriod.Quarter => !year ? "{yyyy}" : found.Contains("qq") ? null : "{qq}",
            AutonumPeriod.Month => !year ? "{yyyy}" : found.Contains("mm") ? null : "{mm}",
            _ => null
        };
        if (missing != null)
            throw new InvalidOperationException(
                $"{head} restarts every {autonum.Period.ToString().ToLowerInvariant()}, so it has to carry {missing} - without it one number is issued in two periods");
    }

    /* Sorted by key: a numbering is addressed by its key alone and its position says nothing, so a
     * reordered file must not move the fingerprint. 'name' defaults as a set value's does; the
     * period is stored by NAME (AutonumDefaultColumns), never by the enum's number.
     */
    internal static IReadOnlyList<SeedRow> Rows(TableMetadata registry) =>
        [.. registry.Autonums.OrderBy(a => a.Id, StringComparer.Ordinal).Select(a =>
            new SeedRow(a.Id, new Dictionary<String, String?>
            {
                [Constants.FieldNames.Name] = a.Name ?? $"@[{registry.Model}.{a.Id}]",
                [Constants.FieldNames.Pattern] = a.Pattern,
                [Constants.FieldNames.Period] = a.Period.ToString()
            }))];
}
