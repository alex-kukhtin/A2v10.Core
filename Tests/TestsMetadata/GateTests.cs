// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* The gate: the line a2security.[Permission.Check] in every batch of a metadata endpoint, with the right
 * the batch needs. See a2v10-md-skill, permissions.md -> "Перевірка".
 */
public class GateTests
{
    static async Task<SqlBuilder> BuilderOf(String url, Boolean useGrants)
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var platformUrl = new PlatformUrl($"_page{url}");
        var (schema, table) = DatabaseMetadataProvider.ParsePath(platformUrl.LocalPath);
        var descriptor = new BuilderDescriptor()
        {
            Endpoint = await provider.GetNormalEndpointAsync(null, schema, table),
            PlatformUrl = platformUrl,
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = useGrants,
        };
        return new SqlBuilder(descriptor, TestHost.Services);
    }

    // the flags are spelled twice, here and in the procedure's case: a misspelt one would read as a refusal
    static String Procedure()
    {
        var script = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../Platform/SqlScripts/a2v10_security_simple.sql")));
        var start = script.IndexOf("create or alter procedure a2security.[Permission.Check]");
        Assert.True(start >= 0, "no a2security.[Permission.Check] in the platform script");
        return script[start..script.IndexOf("\ngo", start)];
    }

    [Fact]
    public void The_gate_names_the_endpoint_and_the_flag()
    {
        var sql = Gate.View.Sql("/catalog/agent");

        Assert.Contains("exec a2security.[Permission.Check] @UserId = @UserId, @Url = N'/catalog/agent', @Flag = N'View';", sql);
        Assert.DoesNotContain("declare", sql);
    }

    // asked of the table: an id that was sent but is not there is a create, not an edit
    [Fact]
    public async Task The_save_is_an_edit_only_of_a_row_already_in_the_table()
    {
        var builder = await BuilderOf("/catalog/agent/edit/new", useGrants: true);
        var table = (await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "catalog", "agent")).Storage;

        var sql = builder.SaveGate().Sql("/catalog/agent");

        Assert.Contains($"declare @_flag nvarchar(16) = case when exists(select 1 from {table.SqlTableName} where [Id] = (select [Id] from @{table.Model})) then N'Edit' else N'Create' end;", sql);
        Assert.Contains("@Flag = @_flag;", sql);
    }

    [Fact]
    public void Every_flag_the_gate_sends_is_one_the_procedure_knows()
    {
        var procedure = Procedure();
        var sent = String.Concat(new[] { Gate.View, Gate.Create, Gate.Delete, Gate.Post, Gate.Unpost, Gate.Save("t", "@Id") }
            .Select(g => g.Sql("/x")));

        foreach (var flag in new[] { "View", "Create", "Edit", "Delete", "Post", "Unpost" })
        {
            Assert.Contains($"N'{flag}'", sent);
            Assert.Contains($"when N'{flag}' then @can{flag}", procedure);
        }
    }

    // in the text, after its 'set's: the batch carries it, not whoever runs the batch
    [Theory]
    [InlineData("/catalog/agent/edit/5", "View")]
    [InlineData("/catalog/agent/edit/new", "Create")]
    public async Task A_card_is_a_view_of_a_record_and_a_create_of_a_new_one(String url, String flag)
    {
        var sql = (await BuilderOf(url, useGrants: true)).BuildLoadPlainSqlText();

        var set = sql.IndexOf("set transaction isolation level");
        var gate = sql.IndexOf("exec a2security.[Permission.Check]");
        Assert.True(set >= 0 && gate > set, "the gate is not after the 'set's");
        Assert.Equal(gate, sql.LastIndexOf("exec a2security.[Permission.Check]"));
        Assert.Contains($"@Flag = N'{flag}';", sql);
    }

    // the source is read whole, so its own right is asked too; a document of its own table has no one else's rows
    [Fact]
    public async Task A_birth_is_a_create_and_a_view_of_its_source()
    {
        var builder = await BuilderOf("/document/receipt/edit/new?Op=supplier&BasedOn=5&Base=/document/order", useGrants: true);

        var sql = await builder.BuildBirthSqlTextAsync();

        var create = sql.IndexOf("@Url = N'/document/receipt', @Flag = N'Create';");
        var view = sql.IndexOf("@Url = N'/document/order', @Flag = N'View';");
        Assert.True(create >= 0 && view > create, "no gate of the receiver, then of the source");
        Assert.DoesNotContain("and not (", sql);
    }

    // the basis must be a row of the source named, not of another document sharing its table; with no grants too
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_basis_is_a_row_of_its_source(Boolean useGrants)
    {
        var builder = await BuilderOf("/document/waybillreturn/edit/new?BasedOn=5&Base=/document/waybillout", useGrants);

        var sql = await builder.BuildBirthSqlTextAsync();

        Assert.Contains("where a.[Id] = @BasedOn and not (a.[Operation] in (select [Id] from doc.[Operations] where [Document] = N'waybillout'))", sql);
        Assert.Equal(useGrants, sql.Contains("@Url = N'/document/waybillout', @Flag = N'View';"));
    }

    [Fact]
    public async Task Without_grants_there_is_no_gate()
    {
        var sql = (await BuilderOf("/catalog/agent/edit/5", useGrants: false)).BuildLoadPlainSqlText();

        Assert.DoesNotContain("Permission.Check", sql);
    }

    const String OwnRows = "a.[Operation] in (select [Id] from doc.[Operations] where [Document] = N'waybillin')";

    // another document's row over the shared table is refused, after the gate; and with no grants at all
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_record_of_another_document_is_refused(Boolean useGrants)
    {
        var sql = (await BuilderOf("/document/waybillin/edit/5", useGrants)).BuildLoadPlainSqlText();

        var check = sql.IndexOf($"where a.[Id] = @Id and not ({OwnRows}))");
        Assert.True(check > sql.IndexOf("set transaction isolation level"), "no record check after the 'set's");
        Assert.True(check > sql.IndexOf("Permission.Check"), "the record check is ahead of the gate");
    }

    // nothing is stored under a new card yet; a document of its own table has no one else's rows
    [Theory]
    [InlineData("/document/waybillin/edit/new")]
    [InlineData("/document/order/edit/5")]
    public async Task No_record_check_where_there_is_nothing_foreign(String url)
    {
        var sql = (await BuilderOf(url, useGrants: true)).BuildLoadPlainSqlText();

        Assert.DoesNotContain("and not (", sql);
    }

    // a row id of another record matches nothing and is inserted here: the other record's row is never updated
    [Theory]
    [InlineData("/catalog/agent/edit/5")]       // collections without kinds
    [InlineData("/document/waybillin/edit/5")]  // a collection split by kinds
    public async Task A_row_is_merged_only_into_its_own_record(String url)
    {
        var builder = await BuilderOf(url, useGrants: false);
        var (schema, name) = DatabaseMetadataProvider.ParsePath(new PlatformUrl($"_page{url}").LocalPath);
        var table = (await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, schema, name)).Storage;

        var sql = builder.MergeDetailsSql();

        Assert.NotEmpty(table.Details);
        foreach (var rows in table.Details.Values)
        {
            var merge = sql.IndexOf($"merge {rows.SqlTableName} as t");
            Assert.True(merge >= 0, $"no merge into {rows.SqlTableName}");
            var on = sql.IndexOf("on t.", merge);
            Assert.Equal($"on t.[Id] = s.[Id] and t.[{rows.MasterField}] = @Id", sql[on..sql.IndexOfAny(['\r', '\n'], on)]);
        }
    }

    [Fact]
    public async Task The_save_keeps_an_operation_of_this_document()
    {
        var sql = (await BuilderOf("/document/waybillin/edit/5", useGrants: false)).SentOperationCheck();

        Assert.Contains($"if exists(select 1 from @Document s where not ({OwnRows.Replace("a.[", "s.[")}))", sql);
        Assert.Contains("N'UI:@[UIError.AccessDenied]'", sql);
    }
}
