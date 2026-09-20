// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The beam itself. What each stage checks is checked by the stages' own tests (the bake, the
 * generated XAML, the print SQL); what is pinned here is the only law the validator owns - the
 * chain stops at the first throw, the map lists the stages THIS endpoint has whether they ran or
 * not, and a finding comes back as a RESULT instead of an exception.
 *
 * The last one is the whole point of the shape: an escaping exception means "the tool could not
 * run", and a validator that let its own stage's throw out would destroy that distinction.
 */
public class EndpointValidateTests
{
    static Task<EndpointValidation> ValidateAsync(String endpoint) =>
        new EndpointValidator(
            TestHost.GetService<DatabaseMetadataProvider>(),
            TestHost.GetService<IAppCodeProvider>(),
            TestHost.Services).ValidateAsync(endpoint);

    static Dictionary<String, String> Checks(String declaration, String screen, String print) =>
        new() { ["declaration"] = declaration, ["screen"] = screen, ["print"] = print };

    // a document with two blanks: all three stages have something to build, none of them vacuous
    [Fact]
    public async Task An_endpoint_that_assembles_reports_every_stage_passed()
    {
        var result = await ValidateAsync("/document/waybillin");

        Assert.Null(result.Error);
        Assert.Equal(Checks("passed", "passed", "passed"), result.Checks);
    }

    /* Two keys and not three: which stages exist follows the endpoint's TYPE, and a report has no
     * blank of its own to resolve. A 'print' key here - with any status - would be the map claiming
     * a stage that cannot exist.
     */
    [Fact]
    public async Task A_report_is_checked_by_its_own_stages()
    {
        var result = await ValidateAsync("/report/stockturnover");

        Assert.Null(result.Error);
        Assert.Equal(new Dictionary<String, String>()
        {
            ["declaration"] = "passed",
            ["screen"] = "passed"
        }, result.Checks);
    }

    /* A finding at a stage past the load: the report's page is built for its declared picks, and a
     * filter naming a column the surface does not have is refused while the page is assembled -
     * reported as a result, with the stage that produced it named by the single 'failed'.
     */
    [Fact]
    public async Task A_stage_that_throws_is_a_finding_and_not_an_exception()
    {
        var result = await ValidateAsync("/report/badfilter");

        Assert.Equal(new Dictionary<String, String>()
        {
            ["declaration"] = "passed",
            ["screen"] = "failed"
        }, result.Checks);
        Assert.False(String.IsNullOrEmpty(result.Error));
    }

    /* 'unknown' and not a missing key: a stage that was requested and never reached has to read on
     * the spot, or its absence is read as consent. The fixture is an 'edit' form with no 'is' - the
     * load has no opinion on it, the screen it builds throws, and 'print' never runs.
     */
    [Fact]
    public async Task A_stage_that_throws_stops_the_chain_and_leaves_the_rest_unknown()
    {
        var result = await ValidateAsync("/catalog/noformkind");

        Assert.Equal(Checks("passed", "failed", "unknown"), result.Checks);
        Assert.False(String.IsNullOrEmpty(result.Error));
    }

    /* One key, and the rest are not 'unknown' but ABSENT: the load is what says which stages an
     * address has, so a load that failed established nothing to be unknown about. Listing three
     * would be the map of a data endpoint printed over something that was never shown to be one.
     */
    [Fact]
    public async Task A_load_that_fails_names_no_further_stages()
    {
        var result = await ValidateAsync("/catalog/nosuch");

        Assert.Equal(new Dictionary<String, String>() { ["declaration"] = "failed" }, result.Checks);
        Assert.False(String.IsNullOrEmpty(result.Error));
    }
}
