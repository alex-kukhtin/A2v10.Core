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

    static async Task<String> SqlAsync()
    {
        var builder = Assert.IsType<TrialBalanceReportBuilder>(BaseReportBuilder.Create(TestHost.Services,
            await EndpointAsync("trialbycompany"), new AppPlatformId(typeof(Int64)), useGrants: false));
        builder.SetGrouping(new ExpandoObject());
        return builder.CreateSqlText();
    }

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
        // typed by the filter's column, as a record's 'sql' initial is
        var type = (await EndpointAsync("trialbycompany")).Surface.AllColumns().First(c => c.Name == "Company").SqlDataType();

        Assert.Contains($"""
            if isnull(@Run, 0) = 0
            begin
            declare @InitCompany {type} = (select min([Id]) from cat.Companies where [Void] = 0);
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

    // no registry behind 'profile' yet: unrefused, the batch read an undeclared @InitCompany
    [Fact]
    public async Task A_start_from_the_profile_is_refused_at_load()
    {
        var message = await RefusedAsync(new ReportMetadata()
        {
            Type = "trialBalance",
            Filters = ["Company"],
            InitialValues = new() { ["Company"] = new InitialMetadata(InitialSource.Profile, "Company") }
        });

        Assert.Contains("not 'Profile'", message);
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
