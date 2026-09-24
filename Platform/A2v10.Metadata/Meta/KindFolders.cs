// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace A2v10.Metadata;

/* app.json 'aliases': more folders of one kind - "aliases": { "document": ["sale", "purchase"] } -
 * for a kind whose endpoints number in the hundreds. An alias is a first segment of an address like
 * the kind's own: sale/invoice/metadata.json is /sale/invoice, of kind document. Nothing is
 * rewritten, so the one question answered here is what a folder IS; the name of an endpoint (an
 * operation's Id) stays the last segment and does not carry the alias.
 *
 * Refused while the map is built, each naming what it broke: a key that is not a kind a folder
 * declares; an alias that is a name the platform gives a first segment itself, which it would
 * shadow or be shadowed by; one folder listed twice.
 */
internal sealed class KindFolders
{
    private readonly Dictionary<String, String> _kindOf;

    private KindFolders(Dictionary<String, String> kindOf) => _kindOf = kindOf;

    // the kind's own folder, and any folder nobody aliased, answer as they are
    internal String KindOf(String folder) =>
        _kindOf.TryGetValue(folder, out var kind) ? kind : folder;

    // read off the constants, so that a namespace added there is refused here without anyone remembering to
    private static readonly HashSet<String> _reserved = [.. typeof(Constants.SchemaNames)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (String)f.GetValue(null)!)];

    // lower case, as ParsePath hands the folder over
    internal static KindFolders From(IReadOnlyDictionary<String, String[]>? aliases)
    {
        var kindOf = new Dictionary<String, String>();
        if (aliases == null)
            return new KindFolders(kindOf);
        foreach (var (key, folders) in aliases)
        {
            var kind = key.ToLowerInvariant();
            if (DatabaseMetadataProvider.EndpointKindOf(kind) == EndpointKind.Undefined)
                throw new InvalidOperationException($"""
                    app.json: 'aliases' names '{key}', which is not a kind.
                      The key is the kind whose folders follow: "aliases": {"{"} "document": ["sale", "purchase"] {"}"}.
                    """);
            foreach (var alias in folders)
            {
                var folder = alias.ToLowerInvariant();
                if (_reserved.Contains(folder))
                    throw new InvalidOperationException($"""
                        app.json: 'aliases' gives '{kind}' the folder '{alias}', which is a name of the platform's own.
                          An alias is a folder of the application; name it after what its endpoints are about ('sale', 'purchase').
                        """);
                if (kindOf.TryGetValue(folder, out var other))
                    throw new InvalidOperationException($"""
                        app.json: 'aliases' lists the folder '{alias}' twice - under '{other}' and under '{kind}'.
                          A folder is of one kind. Keep one.
                        """);
                kindOf.Add(folder, kind);
            }
        }
        return new KindFolders(kindOf);
    }
}
