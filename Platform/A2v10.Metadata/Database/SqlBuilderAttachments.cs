// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The attachments of one owner under one base: rows of a2blob.BlobLinks, read by base + owner and
 * never by the link id alone - a foreign base finds nothing (design/blobs.md, "Protection lives in
 * procedures"). The base arrives from the client, so it is a parameter everywhere, the gate included;
 * the reader has already refused one that names no endpoint. The right is the base's own: an
 * attachment is a part of the record it hangs on
 */
internal class SqlBuilderAttachments(IServiceProvider serviceProvider, AppPlatformId _platformId, Boolean _useGrants)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();

    internal const String OwnerModel = "Owner";
    internal const String Collection = "Attachments";

    String GateSql(Gate gate) => _useGrants ? gate.SqlAt($"@{Constants.FieldNames.BaseQuery}") : String.Empty;

    // the dialog's own context comes back as an object, so its commands post the pair they were opened with
    public Task<IDataModel> LoadAsync(String? dataSource, String? owner, String? @base)
    {
        var sql = $"""
            set nocount on;
            set transaction isolation level read uncommitted;
            {GateSql(Gate.View)}

            select [{OwnerModel}!TOwner!Object] = null, [Id!!Id] = @Id, [{Constants.FieldNames.BaseQuery}] = @Base;

            select [{Collection}!TAttachment!Array] = null, [Id!!Id] = l.Id, [Blob!{Constants.Blobs.TypeName}!RefId] = l.[Blob]
            from a2blob.BlobLinks l
            where l.[Base] = @Base and l.LinkId = @Id
            order by l.Id;

            select [!{Constants.Blobs.TypeName}!Map] = null, [Id!!Id] = b.Id, b.[Name], b.Mime, [Token!!Token] = b.AccessToken
            from {Constants.Blobs.Table} b
              inner join a2blob.BlobLinks l on b.Id = l.[Blob]
            where l.[Base] = @Base and l.LinkId = @Id;
            """;
        return _dbContext.LoadModelSqlAsync(dataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms.AddTyped("@Id", _platformId.SqlDbType, _platformId.ParseId(owner));
            dbprms.AddString("@Base", @base);
        });
    }

    /* By owner + base + link, never by the link id alone: a foreign base or owner removes nothing. The
     * link goes, the blob stays - no explicit delete, an orphan (design/blobs.md). The right is Edit of
     * the base: the record changes, it is not deleted
     */
    public async Task<IInvokeResult> DeleteAsync(String? dataSource, ExpandoObject? prms)
    {
        var sql = $"""
            set nocount on;
            set transaction isolation level read committed;
            {GateSql(Gate.Edit)}

            delete from a2blob.BlobLinks where Id = @Id and [Base] = @Base and LinkId = @Owner;
            """;
        await _dbContext.LoadModelSqlAsync(dataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms.AddBigInt("@Id", Convert.ToInt64(prms?.Get<Object>("Id")));
            dbprms.AddString("@Base", prms?.Get<String>(Constants.FieldNames.BaseQuery));
            dbprms.AddTyped("@Owner", _platformId.SqlDbType, _platformId.ParseId(prms?.Get<Object>("Owner")?.ToString()));
        });
        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }
}
