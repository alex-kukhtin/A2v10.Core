// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* Every action of Constants.Actions.All is named by every switch that reads the action - the template and
 * the XAML here; the model needs a database. Four switches never saw each other, and an action added to one
 * and forgotten in another reached the user as an exception on a click. An action not built yet throws
 * NotImplementedException from its own arm; only a forgotten one reaches the default arm (Unknown).
 */
public class ActionDispatchTests
{
    static async Task<NormalEndpointMetadata> EndpointAsync(String schema, String table) =>
        await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, schema, table);

    // one address per action, on a TestApp endpoint that has it; an action with none here is a test to extend
    static async Task<(NormalEndpointMetadata Endpoint, IPlatformUrl Url)> UrlFor(String action)
    {
        var item = await EndpointAsync("catalog", "item");   // folders
        var waybill = await EndpointAsync("document", "waybillin");   // posts and prints
        return action switch
        {
            Constants.Trans.Action => (waybill, waybill.PlatformUrl(action)),
            Constants.Print.Action => (waybill, waybill.PlatformUrl(action,
                $"{Constants.Print.FormQuery}={waybill.Declaration.PrintForms.First().Name}")),
            _ when Constants.Actions.All.Contains(action) => (item, item.PlatformUrl(action)),
            _ => throw new InvalidOperationException($"no address for '{action}' in the test")
        };
    }

    [Fact]
    public async Task Every_action_is_named_by_the_template_and_the_xaml()
    {
        var platformId = await TestHost.GetService<DatabaseMetadataProvider>().DeclaredPlatformIdAsync()
            ?? throw new InvalidOperationException("TestApp/app.json declares no platformid");

        var forgotten = new List<String>();
        foreach (var action in Constants.Actions.All)
        {
            var (endpoint, url) = await UrlFor(action);
            var builder = new BaseModelBuilder(TestHost.Services, new BuilderDescriptor()
            {
                Endpoint = endpoint,
                PlatformUrl = url,
                PlatformId = platformId,
                UseGrants = false
            });
            foreach (var (half, run) in new (String, Func<Task>)[]
            {
                ("template", () => builder.CreateTemplateAsync()),
                ("xaml", () => Task.FromResult(builder.CreateDefaultXamlForm()))
            })
            {
                try
                {
                    await run();
                }
                catch (NotImplementedException) when (action == Constants.Actions.Show)
                {
                    // declared not built
                }
                catch (Exception ex)
                {
                    forgotten.Add($"{action} {half}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        Assert.True(forgotten.Count == 0, String.Join("\n", forgotten));
    }
}
