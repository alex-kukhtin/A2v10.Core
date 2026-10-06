// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace A2v10.Metadata;

internal sealed record AppRole(String Id, String Name, String? Memo);

/* app.json 'roles': the closed dictionary of the application's roles -
 * "roles": { "Accountant": { "Name": "Бухгалтер", "Memo": "..." } }. A map and not an array: the roles
 * have no order, and the key is the one 'grants' names. Not a kind of its own: no address, no screen,
 * no fields - a list beside 'useGrants'.
 *
 * The rows of a2security.Roles, a table of the platform script: the deploy writes the rows and never
 * the table (SqlDbGenerator.CreateRolesScript). Everything is checked here, without a database, so a
 * wrong file fails the load and not the deploy.
 */
internal static partial class AppRoles
{
    // predefined: the platform script inserts it, and its rights cannot lie in what the deploy writes
    internal const String Admin = "Admin";
    /* predefined as well: every user holds it without an assignment, so 'grants' can open an
     * endpoint to all in one line - and a forgotten line keeps it closed, never open
     */
    internal const String Everyone = "Everyone";

    // the platform script's rows: never declared, never voided by the merge
    internal static readonly String[] Predefined = [Admin, Everyone];

    // the columns of a2security.Roles (a2v10_struct_simple.sql)
    private const Int32 IdLength = 64;
    private const Int32 TextLength = 255;

    private static readonly String[] _columns = [Constants.FieldNames.Name, Constants.FieldNames.Memo];

    // a name the author gives, as a field's is - not an address segment; compared ordinal
    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalCase();

    // null - no 'roles' at all: the application declared none, and the deploy leaves the table alone
    internal static IReadOnlyList<AppRole>? From(IReadOnlyDictionary<String, Dictionary<String, JToken?>?>? roles)
    {
        if (roles == null)
            return null;
        return [.. roles.Select(kv => Role(kv.Key, kv.Value)).OrderBy(r => r.Id, StringComparer.Ordinal)];
    }

    private static AppRole Role(String id, Dictionary<String, JToken?>? row)
    {
        if (Predefined.Contains(id))
            throw new InvalidOperationException($"""
                app.json: 'roles' declares '{id}'. The role is the platform's own: the platform script inserts it - '{Admin}' passes everything, '{Everyone}' is every user.
                  Remove it from the map.
                """);
        if (!PascalCase().IsMatch(id))
            throw new InvalidOperationException($"""
                app.json: 'roles' declares '{id}'. A role is written in PascalCase - 'Accountant', not 'accountant': 'grants' names it the same way, compared exactly.
                """);
        if (id.Length > IdLength)
            throw new InvalidOperationException($"app.json: role '{id}' - the key is longer than {IdLength}");
        if (row == null)
            throw new InvalidOperationException($"app.json: role '{id}' has no columns. Write at least \"Name\"");

        if (row.Keys.FirstOrDefault(k => !_columns.Contains(k)) is String key)
            throw new InvalidOperationException(
                $"app.json: role '{id}' names '{key}'. A role has {String.Join(" and ", _columns.Select(c => $"'{c}'"))} only");

        String? Text(String column)
        {
            var token = row.GetValueOrDefault(column);
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type != JTokenType.String)
                throw new InvalidOperationException($"app.json: role '{id}' - '{column}' is a {token.Type}, a string is expected");
            var value = token.Value<String>()!;
            if (value.Length > TextLength)
                throw new InvalidOperationException($"app.json: role '{id}' - '{column}' is longer than {TextLength}");
            return value;
        }

        var name = Text(Constants.FieldNames.Name);
        if (String.IsNullOrEmpty(name))
            throw new InvalidOperationException($"app.json: role '{id}' has no 'Name'");
        return new AppRole(id, name, Text(Constants.FieldNames.Memo));
    }
}
