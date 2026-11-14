// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A required filter and the start of a filter. The refusal is the batch's, so the page and report_query
 * meet one judge; the start is the open's (no Run), so a run is exactly its url.
 */
public class ReportFilterTests
{
    static async Task<ReportEndpointMetadata> EndpointAsync(String report) =>
        Assert.IsType<ReportEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, "report", report));

    static async Task<String> SqlAsync(String report = "trialbycompany")
    {
        var builder = Assert.IsType<TrialBalanceReportBuilder>(BaseReportBuilder.Create(TestHost.Services,
            await EndpointAsync(report), new AppPlatformId(typeof(Int64)), useGrants: false));
        builder.SetGrouping(new ExpandoObject());
        return builder.CreateSqlText();
    }

    // a variable is typed by the filter's column, as a record's initial is
    static String CompanyType() =>
        TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "ledger", "national").Result
            .Storage.AllColumns().First(c => c.Name == "Company").SqlDataType();

    [Fact]
    public async Task A_run_without_a_required_filter_is_refused_by_the_batch()
    {
        var sql = await SqlAsync();

        Assert.Contains($"""
            if @Run = 1 and (@$Company is null)
                {SqlExtensions.UserError("Report.Error.FilterRequired")}
            """, sql);
    }

    [Fact]
    public async Task A_filter_starts_on_open_and_a_run_takes_its_url()
    {
        var sql = await SqlAsync();

        Assert.Contains($"""
            if isnull(@Run, 0) = 0
            begin
            declare @InitCompany {CompanyType()} = (select min([Id]) from cat.Companies where [Void] = 0);
            set @$Company = isnull(@$Company, @InitCompany);
            end;
            """, sql);
    }

    // the schema takes both keys, and the page is built with them
    [Fact]
    public async Task A_report_with_a_required_filter_passes_every_stage()
    {
        var result = await new EndpointValidator(
            TestHost.GetService<DatabaseMetadataProvider>(),
            TestHost.GetService<A2v10.Infrastructure.IAppCodeProvider>(),
            TestHost.Services,
            TestHost.GetService<A2v10.Infrastructure.ILocalizer>()).ValidateAsync("/report/trialbycompany");

        Assert.Null(result.Error);
    }

    static async Task<String> RefusedAsync(ReportMetadata report)
    {
        var surface = (await EndpointAsync("trialbycompany")).Surface;
        return Assert.Throws<InvalidOperationException>(() => report.Check("report/x/metadata.json", surface)).Message;
    }

    [Fact]
    public async Task A_required_name_not_offered_is_refused_at_load()
    {
        var message = await RefusedAsync(new ReportMetadata() { Type = "trialBalance", Filters = ["Company"], Required = ["Agent"] });

        Assert.Contains("required: 'Agent' is not in 'filters'", message);
    }

    [Fact]
    public async Task A_start_of_a_name_not_offered_is_refused_at_load()
    {
        var message = await RefusedAsync(new ReportMetadata()
        {
            Type = "trialBalance",
            Filters = ["Company"],
            InitialValues = new() { ["Agent"] = new InitialMetadata(InitialSource.Sql, "1") }
        });

        Assert.Contains("initialValues: 'Agent' is not in 'filters'", message);
    }

    // the read a record's birth makes, inside the open's block
    [Fact]
    public async Task A_filter_starts_on_the_users_profile()
    {
        var sql = await SqlAsync("trialbyprofile");

        Assert.Contains($"""
            if isnull(@Run, 0) = 0
            begin
            declare @InitCompany {CompanyType()};
            select @InitCompany = [Company] from {TableMetadataDefaults.ProfileSqlTable} where [Id] = @UserId;
            set @$Company = isnull(@$Company, @InitCompany);
            end;
            """, sql);
    }

    // a slot of another catalog put one catalog's key into a reference to another
    [Fact]
    public async Task A_slot_of_another_type_is_refused_at_load()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var profile = (await provider.GetNormalEndpointAsync(null, "profile", String.Empty)).Storage;
        var company = (await provider.GetNormalEndpointAsync(null, "ledger", "national")).Storage.AllColumns().First(c => c.Name == "Company");

        var wrong = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckProfileSlot("/report/x", "Store", profile, company));
        var missing = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckProfileSlot("/report/x", "Firm", profile, company));
        DatabaseMetadataProvider.CheckProfileSlot("/report/x", "Company", profile, company);

        Assert.Contains("reads the profile slot 'Store', a 'Ref' to /catalog/store; 'Company' is a 'Company' to /catalog/company", wrong.Message);
        Assert.Contains("does not declare. Slots: Store, Agent, Company, MainCashAccount, VatRate", missing.Message);
    }

    // the check of a record's initial, not a copy of it
    [Fact]
    public async Task A_login_does_not_start_a_company()
    {
        var message = await RefusedAsync(new ReportMetadata()
        {
            Type = "trialBalance",
            Filters = ["Company"],
            InitialValues = new() { ["Company"] = new InitialMetadata(InitialSource.Context, "currentUser") }
        });

        Assert.Contains("'currentUser' is a login", message);
    }
}
