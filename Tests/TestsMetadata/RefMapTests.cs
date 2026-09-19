// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* What a loaded row's reference resolves into. The builder turns metadata into text and holds no
 * IDbContext, so it is assertable without DI - TESTS.md, layer D.
 *
 * It earns a test file of its own because of how it fails: a field the map does not carry is not an
 * error anywhere. The binding finds nothing, the badge renders without a colour, and the page looks
 * finished.
 */
public class RefMapTests
{
    static async Task<String> ResolvesOf(String schema, String table)
    {
        var endpoint = (NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, schema, table);
        return new RefMapBuilder(endpoint, isPlain: true, hasDefaults: false).GenerateResolves()
            ?? throw new InvalidOperationException("no resolves");
    }

    /* Two sets are referenced from the same document - one of values, one of states - so the two
     * halves of the rule are visible in one text: the colour and the role travel for the second and
     * are not invented for the first.
     */
    [Fact]
    public async Task A_state_resolves_with_its_colour_and_its_role()
    {
        var sql = await ResolvesOf("document", String.Empty);

        var states = sql.Split("with T as").Single(s => s.Contains("state.[OrderStates]"));
        // the exact text, because the fields are spliced between two others: a comma too many
        // or too few is a syntax error nothing here would otherwise notice
        Assert.Contains("[Id!!Id] = a.Id, [Name!!Name] = a.[Name], a.[Color], a.[Role]", states);

        var rates = sql.Split("with T as").Single(s => s.Contains("enm.[VatRates]"));
        Assert.DoesNotContain("[Color]", rates);
        Assert.DoesNotContain("[Role]", rates);
    }

    // the presentation is what 'Name' carries, and a set is shown by its own Name like any target
    [Fact]
    public async Task Every_target_still_resolves_to_an_id_and_a_name()
    {
        var sql = await ResolvesOf("document", String.Empty);

        foreach (var block in sql.Split("with T as").Skip(1))
            Assert.Contains("[Id!!Id] = a.Id, [Name!!Name] = a.[", block);
    }
}
