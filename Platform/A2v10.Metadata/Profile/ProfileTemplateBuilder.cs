// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The script of /_profile. A section is read until 'Change', and one section is edited at a time:
 * Cancel reloads the model, which would drop the other section's edit. The names of the template
 * commands are shared with XamlProfileBuilder, the server's with ProfilePageBuilder.
 */
internal static class ProfileTemplateBuilder
{
    internal const String EditPersonal = "editPersonal";
    internal const String EditDefaults = "editDefaults";
    internal const String UpdatePersonal = "updatePersonal";
    internal const String UpdateDefaults = "updateDefaults";
    internal const String Cancel = "cancel";

    // the section being edited, a $$ property: switching is no edit
    private const String Edit = "$$Edit";
    private const String Personal = "Personal";
    private const String Defaults = "Defaults";

    // what the page binds to: nothing edited, and which section is
    internal const String Viewing = "Root.$Viewing";
    internal const String EditingPersonal = "Root.$EditPersonal";
    internal const String EditingDefaults = "Root.$EditDefaults";

    /* What the page offers, for the page and for the server alike. A choice of one is none: one locale
     * configured, or no dark theme beside the light one, and there is no control at all.
     */
    internal static IReadOnlyCollection<String> Locales(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IGlobalization>().AvailableLocales is { Count: > 1 } locales ? locales : [];

    internal static IReadOnlyCollection<String> Themes(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IApplicationTheme>().IsDarkThemeEnabled
            ? [UserTheme.Light, UserTheme.Dark, UserTheme.Auto]
            : [];

    internal static String ThemeLabel(String theme) => theme switch
    {
        UserTheme.Light => "@[Profile.Theme.Light]",
        UserTheme.Dark => "@[Profile.Theme.Dark]",
        UserTheme.Auto => "@[Profile.Theme.Auto]",
        _ => throw new InvalidOperationException($"'{theme}' is not a theme")
    };

    // a backtick: the translation may carry an apostrophe ("Обов'язкове"), which ends a quoted string
    internal static String Template => $$"""
        const template = {
            properties: {
                'TRoot.{{Edit}}': String,
                'TRoot.$Viewing'() { return !this.{{Edit}}; },
                'TRoot.$EditPersonal'() { return this.{{Edit}} === '{{Personal}}'; },
                'TRoot.$EditDefaults'() { return this.{{Edit}} === '{{Defaults}}'; }
            },
            validators: {
                'User.PersonName': `@[Error.Required]`
            },
            commands: {
                {{EditPersonal}}() { this.{{Edit}} = '{{Personal}}'; },
                {{EditDefaults}}() { this.{{Edit}} = '{{Defaults}}'; },
                {{UpdatePersonal}},
                {{UpdateDefaults}},
                {{Cancel}}
            }
        };

        module.exports = template;

        // the name in the header, the language and the theme are the sign-in's: the shell is drawn again
        async function {{UpdatePersonal}}() {
            const user = this.User;
            await this.$ctrl.$invoke('{{ProfilePageBuilder.SetPersonalCommand}}',
                { PersonName: user.PersonName, Locale: user.Locale, Theme: user.Theme });
            window.location.reload();
        }

        async function {{UpdateDefaults}}() {
            await this.$ctrl.$save();
            this.{{Edit}} = '';
        }

        async function {{Cancel}}() {
            this.{{Edit}} = '';
            await this.$ctrl.$reload();
        }
        """;
}
