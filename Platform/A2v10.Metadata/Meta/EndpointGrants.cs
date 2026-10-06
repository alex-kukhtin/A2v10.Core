// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

// a row of a2security.Grants without its endpoint: the endpoint is whoever holds the list
public sealed record EndpointGrant(String Role, PermissionFlag Flags);

/* 'grants' of an endpoint's metadata.json - "grants": { "Accountant": ["view", "edit"] } - as the rows
 * the deploy writes (SqlDbGenerator.CreateGrantsScript). Read, not checked: an unknown verb fails only
 * because it has no bit to land on, and a role missing from the dictionary fails the deploy on the key
 * of a2security.Grants to a2security.Roles.
 */
internal static class EndpointGrants
{
    /* By value: CanPost is CanApply and CanUnpost is CanUnapply, one bit under two names, so the bit
     * is never found by its name.
     */
    private static readonly Dictionary<String, PermissionFlag> _verbs = new(StringComparer.Ordinal)
    {
        ["view"] = PermissionFlag.CanView,
        ["create"] = PermissionFlag.CanCreate,
        ["edit"] = PermissionFlag.CanEdit,
        ["delete"] = PermissionFlag.CanDelete,
        ["post"] = PermissionFlag.CanPost,
        ["unpost"] = PermissionFlag.CanUnpost
    };

    // null - the key is not written, which "grants": {} is not
    internal static IReadOnlyList<EndpointGrant>? From(String fileName, Dictionary<String, String[]>? grants) =>
        grants?.Select(kv => new EndpointGrant(kv.Key, kv.Value.Aggregate(default(PermissionFlag), (flags, verb) => flags | Verb(fileName, kv.Key, verb)))).ToList();

    private static PermissionFlag Verb(String fileName, String role, String verb) =>
        _verbs.TryGetValue(verb, out var flag)
            ? flag
            : throw new InvalidOperationException(
                $"{fileName}: 'grants' gives '{role}' the verb '{verb}'. The verbs are {String.Join(", ", _verbs.Keys.Select(k => $"'{k}'"))}, lower case");
}
