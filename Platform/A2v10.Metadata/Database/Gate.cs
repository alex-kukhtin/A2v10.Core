// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* The right a batch needs on its endpoint, as a2security.[Permission.Check] spells it. A flag for every
 * act but the save, which is two: an edit when the row is already in the table, a create otherwise.
 * Asked of the table, never of the id that was sent: a made-up id would pass as an edit, and the merge
 * would insert it.
 *
 * A line of the batch's own text, right after its 'set's - not a prefix glued on where it is run: an
 * ejected batch must carry its gate.
 */
internal sealed class Gate
{
    private readonly String _flag;
    private readonly String? _computed;
    private Gate(String flag, String? computed = null) => (_flag, _computed) = (flag, computed);

    internal static readonly Gate View = new("N'View'");
    internal static readonly Gate Create = new("N'Create'");
    internal static readonly Gate Delete = new("N'Delete'");
    internal static readonly Gate Post = new("N'Post'");
    internal static readonly Gate Unpost = new("N'Unpost'");

    // exec takes no expression, so the fork goes through a variable - one per batch, a batch saves one record
    internal static Gate Save(String sqlTable, String id) =>
        new("@_flag", $"case when exists(select 1 from {sqlTable} where [Id] = {id}) then N'Edit' else N'Create' end");

    // @UserId is passed by every batch
    internal String Sql(String path)
    {
        var exec = $"exec a2security.[Permission.Check] @UserId = @UserId, @Url = N'{path}', @Flag = {_flag};";
        return _computed == null
            ? $"""

            {exec}

            """
            : $"""

            declare @_flag nvarchar(16) = {_computed};
            {exec}

            """;
    }
}
