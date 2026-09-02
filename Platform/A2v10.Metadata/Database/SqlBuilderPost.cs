// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

/* The frame both spellings of 'post' run in, and the only place that owns it.
 *
 * The flag IS the lock, claimed by the statement that reads it. Reading it first and inserting
 * after left the window open: two clicks or a retry after a timeout both saw Done = 0, both
 * inserted - and unpost deletes by provenance, so the doubled rows do not even surface as
 * leftovers. Nothing stands under it in the schema either - the deploy generates no indexes.
 * @@rowcount = 0 also answers for a document that is not there, which used to fall through both
 * branches of the 'if' and report success over an empty insert.
 *
 * A procedure is called from inside all of this: it never sees a second caller, and it does not own
 * the transaction. It must not commit (which only decrements @@TRANCOUNT) or roll back (which kills
 * the outer one and makes the commit below fail far from the cause) - it fails by throwing. And the
 * document is already in its target state when the procedure runs, so reading Done there answers
 * about the posting in progress.
 */
internal partial class SqlBuilder
{
    internal async Task<IInvokeResult> PostDocumentAsync(ExpandoObject? prms)
    {
        var statements = new PostStatements(Endpoint);

        var postSql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        begin tran;

        update {Table.SqlTableName} set [{Constants.FieldNames.Done}] = 1
        where [{Constants.FieldNames.Id}] = @Id and [{Constants.FieldNames.Done}] = 0;
        if @@rowcount = 0
            throw 600000, N'@[Error.Document.AlreadyPosted]', 0;

        {statements.Post}

        commit tran;
        """;

        await _dbContext.LoadModelSqlAsync(DataSource, postSql, PostParameters(prms));

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }

    internal async Task<IInvokeResult> UnPostDocumentAsync(ExpandoObject? prms)
    {
        var statements = new PostStatements(Endpoint);

        var unPostSql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        begin tran;

        update {Table.SqlTableName} set [{Constants.FieldNames.Done}] = 0
        where [{Constants.FieldNames.Id}] = @Id and [{Constants.FieldNames.Done}] = 1;
        if @@rowcount = 0
            throw 600000, N'@[Error.Document.NotPosted]', 0;

        {statements.UnPost}

        commit tran;
        """;

        await _dbContext.LoadModelSqlAsync(DataSource, unPostSql, PostParameters(prms));

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }

    /* The procedure's two ports, passed whether or not a procedure is called: they are the contract,
     * and a parameter list that changes with the branch is a contract read from the branch. @Id is
     * typed from the platform base rather than declared bigint - uniqueidentifier is a supported
     * layout, and a string parameter would lean on type precedence at every comparison.
     */
    private Action<DbParameterCollection> PostParameters(ExpandoObject? prms) =>
        dbprms => dbprms
            .AddTyped("@Id", PlatformId.SqlDbType, PlatformId.ParseId(prms?.Get<Object>("Id")?.ToString()))
            .AddBigInt("@UserId", _currentUser.Identity.Id);
}
