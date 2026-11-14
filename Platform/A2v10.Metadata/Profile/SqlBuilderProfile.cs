// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The row of /_profile is the current user's, and @UserId is the only key here: nothing from the
 * client names whose row it is. A user who never saved has no row, so the load reads through a left
 * join and the save merges.
 */
internal class SqlBuilderProfile(IServiceProvider _serviceProvider, AppPlatformId _platformId)
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();

    // the account's part of the model; the slots come under the profile's own model (XamlBuilder's card binds them so)
    internal const String User = "User";

    // the slots: what profile/metadata.json declares, the key aside
    internal static IEnumerable<TableColumn> Slots(TableMetadata profile) =>
        profile.AllColumns(c => c.Name != Constants.FieldNames.Id);

    internal static String LoadSqlText(TableMetadata? profile)
    {
        var sb = new StringBuilder($"""
            {SqlBuilderUsers.Head}

            select [{User}!TUser!Object] = null, [Id!!Id] = v.Id, v.UserName, v.PersonName, v.[Locale], v.Theme
            from a2security.ViewUsers v
            where v.Id = @UserId;
            """);
        if (profile == null)
            return sb.ToString();

        var slots = Slots(profile).ToList();
        sb.AppendLine();
        sb.AppendLine($"""

            select [{profile.Model}!{profile.TypeName}!Object] = null, [Id!!Id] = u.[Id], {String.Join(", ", slots.Select(c => c.SqlModelColumnName("p")))}
            from (select [Id] = @UserId) u left join {profile.SqlTableName} p on p.[Id] = u.[Id];
            """);
        new RefMapBuilder([(profile, $"[{Constants.FieldNames.Id}] = @UserId", slots)]).WriteRefMap(sb);
        // a set is picked from the list it rides with, as on a card (XamlBuilder.Editor)
        foreach (var set in slots.Where(c => c.IsSetRef).Select(c => c.RefTableCheck.Storage).DistinctBy(t => t.SqlTableName))
            sb.AppendLine(SqlBuilder.ValuesRecordset(set, withAll: false));
        return sb.ToString();
    }

    internal static String SaveSqlText(TableMetadata profile)
    {
        var slots = Slots(profile).ToList();
        return $"""
            {SqlBuilderUsers.Head}
            set xact_abort on;

            merge {profile.SqlTableName} as t
            using @{profile.Model} as s
            on t.[Id] = @UserId
            when matched then update set
              {String.Join(", ", slots.Select(c => $"t.[{c.Name}] = {c.SentValue("s")}"))}
            when not matched then insert
              ([Id], {String.Join(", ", slots.Select(c => $"[{c.Name}]"))}) values
              (@UserId, {String.Join(", ", slots.Select(c => c.SentValue("s")))});

            """ + LoadSqlText(profile);
    }

    public Task<IDataModel> LoadAsync(String? dataSource, TableMetadata? profile) =>
        _dbContext.LoadModelSqlAsync(dataSource, LoadSqlText(profile), dbprms => dbprms.AddBigInt("@UserId", UserId()));

    // the slots only: the account's part is written by Identity (IAppUserProfile)
    public async Task<ExpandoObject> SaveAsync(String? dataSource, TableMetadata profile, ExpandoObject data)
    {
        var allowEmptyStrings = _serviceProvider.GetRequiredService<IDataConfiguration>().AllowEmptyStrings;
        var table = new DataTableBuilder(profile, _platformId, allowEmptyStrings).BuildDataTable(data.Get<ExpandoObject>(profile.Model));
        var dm = await _dbContext.LoadModelSqlAsync(dataSource, SaveSqlText(profile), dbprms =>
        {
            dbprms.AddBigInt("@UserId", UserId());
            dbprms.AddStructured($"@{profile.Model}", profile.SqlTableTypeName, table);
        }) ?? throw new InvalidOperationException("Profile. Save failed. DataModel is null");
        return dm.Root;
    }

    private Int64 UserId() => _serviceProvider.GetRequiredService<ICurrentUser>().Identity.Id
        ?? throw new InvalidOperationException("Profile. UserId is null");
}
