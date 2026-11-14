// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* /_profile - the user's own page. The SQL needs a database; what is pinned here is that no key but
 * @UserId reaches a row, that the declaration is no address, and the page as text.
 */
public class ProfilePageTests
{
    static async Task<TableMetadata> ProfileAsync() =>
        (await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "profile", String.Empty)).Storage;

    [Fact]
    public async Task The_page_is_a_type_at_the_shells_address()
    {
        var endpoint = await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, "_profile", String.Empty);

        Assert.Equal("/_profile", Assert.IsType<ProfilePageEndpointMetadata>(endpoint).Path);
    }

    // the load reads the current user's row and nothing names another one; a user who never saved reads empty slots
    [Fact]
    public async Task The_load_is_keyed_by_the_current_user()
    {
        var sql = SqlBuilderProfile.LoadSqlText(await ProfileAsync());

        Assert.Contains("from a2security.ViewUsers v\nwhere v.Id = @UserId;", sql.ReplaceLineEndings("\n"));
        Assert.Contains($"from (select [Id] = @UserId) u left join {TableMetadataDefaults.ProfileSqlTable} p on p.[Id] = u.[Id];", sql);
        Assert.Contains("[Store!TRStore!RefId] = p.[Store]", sql);
        Assert.Contains($"from {TableMetadataDefaults.ProfileSqlTable}\nwhere [Id] = @UserId;", sql.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("@Id", sql.Replace("@UserId", String.Empty));
    }

    // the sent Id is ignored: the row merged and inserted is @UserId's
    [Fact]
    public async Task The_save_writes_the_current_users_row()
    {
        var sql = SqlBuilderProfile.SaveSqlText(await ProfileAsync()).ReplaceLineEndings("\n");

        Assert.Contains($"merge {TableMetadataDefaults.ProfileSqlTable} as t\nusing @Profile as s\non t.[Id] = @UserId\n", sql);
        Assert.Contains("([Id], [Store], [Agent], [Company], [MainCashAccount], [VatRate]) values\n  (@UserId, s.[Store]", sql);
        Assert.DoesNotContain("s.[Id]", sql);
    }

    // served as an endpoint, '/profile/edit/<id>' would open anyone's row
    [Fact]
    public async Task The_declaration_is_no_page()
    {
        var url = new PlatformUrl("_page/profile/edit/5");
        var view = await TestHost.GetService<IModelJsonReader>().GetViewAsync(url);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestHost.GetService<IModelBuilderFactory>().BuildAsync(url, view));

        Assert.Contains("the profile is '/_profile'", ex.Message);
    }

    // a set's slot is picked from its values, as on a card: the editor binds the root's collection
    [Fact]
    public async Task A_set_slot_rides_with_its_values()
    {
        var profile = await ProfileAsync();
        var vat = profile.AllColumns().First(c => c.Name == "VatRate").RefTableCheck.Storage;
        var sql = SqlBuilderProfile.LoadSqlText(profile);
        var xaml = await PageAsync(new Globalization("uk-UA"), new Theme(dark: false));

        Assert.Contains($"select [{vat.CollectionName}!{vat.TypeName}!Array] = null", sql);
        Assert.Contains($"ItemsSource=\"{{Bind {vat.CollectionName}}}\"", xaml);
    }

    sealed class Globalization(params String[] locales) : IGlobalization
    {
        public String? DateLocale => null;
        public String? NumberLocale => null;
        public String? IsAvailable(String lang) => null;
        public IReadOnlyCollection<String> AvailableLocales => locales;
    }

    sealed class Theme(Boolean dark) : IApplicationTheme
    {
        public String MakeTheme() => String.Empty;
        public String LogoUrl() => String.Empty;
        public String BodyCssClass => String.Empty;
        public String HtmlCssClass => String.Empty;
        public Boolean IsDarkThemeEnabled => dark;
        public Boolean IsDark => false;
    }

    static async Task<String> PageAsync(Globalization globalization, Theme theme)
    {
        var services = new ServiceCollection()
            .AddSingleton<IGlobalization>(globalization)
            .AddSingleton<IApplicationTheme>(theme)
            .BuildServiceProvider();
        var profile = await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "profile", String.Empty);
        var slots = new XamlBuilder(profile, new PlatformUrl("_page/_profile/index/0")).SlotEditors();
        return XamlTextBulder.GetXaml(new XamlProfileBuilder(services).Page(slots));
    }

    // a slot is edited as on any card: a reference picks from its catalog
    [Fact]
    public async Task A_slot_is_the_cards_editor()
    {
        var xaml = await PageAsync(new Globalization("uk-UA"), new Theme(dark: false));

        Assert.Contains("<SelectorSimple", xaml);
        Assert.Contains("""Value="{Bind Profile.Store}""", xaml);
        Assert.Contains("Url=\"/catalog/store\"", xaml);
        Assert.Contains("""Disabled="{Bind !Root.$EditDefaults}""", xaml);
    }

    // a choice of one is none: no language control for one locale, no theme without a dark one
    [Fact]
    public async Task Only_a_real_choice_is_offered()
    {
        var single = await PageAsync(new Globalization("uk-UA"), new Theme(dark: false));
        var both = await PageAsync(new Globalization("uk-UA", "en-US"), new Theme(dark: true));

        Assert.DoesNotContain("User.Locale", single);
        Assert.DoesNotContain("User.Theme", single);
        Assert.Contains("""Value="{Bind User.Locale}""", both);
        Assert.Contains("Value=\"en-US\"", both);
        Assert.Contains("@[Profile.Theme.Auto]", both);
    }
}
