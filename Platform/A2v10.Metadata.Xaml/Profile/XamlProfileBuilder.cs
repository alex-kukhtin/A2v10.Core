// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

/* /_profile: a section per part, read until 'Change'. A fieldset disables what it holds, the slots'
 * pickers among them, so a section is one control and not a flag per field.
 */
internal class XamlProfileBuilder(IServiceProvider _serviceProvider)
{
    private const String User = SqlBuilderProfile.User;

    private static Button Command(String content, String command, String visible, Boolean primary = false, Boolean validRequired = false) => new()
    {
        Content = content,
        Style = primary ? ButtonStyle.Primary : ButtonStyle.Default,
        Bindings = b =>
        {
            b.SetBinding(nameof(Button.Command), new BindCmdExec(command) { ValidRequired = validRequired });
            b.SetBinding(nameof(Button.If), new Bind(visible));
        }
    };

    private static StackPanel Section(String title, String editing, String edit, String update, Boolean validRequired,
        IEnumerable<UIElementBase> controls) => new()
    {
        Gap = GapSize.FromString("0.5rem"),
        Children = [
            new FieldSet()
            {
                Title = title,
                Orientation = Orientation.Vertical,
                Bindings = b => b.SetBinding(nameof(FieldSet.Disabled), new Bind($"!{editing}")),
                Children = [.. controls]
            },
            new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Gap = GapSize.FromString("0.5rem"),
                Children = [
                    Command("@[Profile.Change]", edit, ProfileTemplateBuilder.Viewing),
                    Command("@[Profile.Update]", update, editing, primary: true, validRequired: validRequired),
                    Command("@[Cancel]", ProfileTemplateBuilder.Cancel, editing)
                ]
            }
        ]
    };

    private static ComboBox Choice(String label, String path, IEnumerable<(String Value, String Content)> items) => new()
    {
        Label = label,
        Children = [.. items.Select(i => new ComboBoxItem() { Value = i.Value, Content = i.Content })],
        Bindings = b => b.SetBinding(nameof(ComboBox.Value), new Bind(path))
    };

    // a language is named in itself, as every language list does
    private static String LocaleName(String locale)
    {
        var culture = CultureInfo.GetCultureInfo(locale);
        return String.Concat(culture.NativeName[..1].ToUpper(culture), culture.NativeName[1..]);
    }

    private IEnumerable<UIElementBase> Personal()
    {
        // shown, not edited: the login is the account's
        yield return new TextBox()
        {
            Label = "@[Admin.Login]",
            Disabled = true,
            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind($"{User}.UserName"))
        };
        yield return new TextBox()
        {
            Label = "@[Admin.PersonName]",
            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind($"{User}.PersonName"))
        };
        var locales = ProfileTemplateBuilder.Locales(_serviceProvider);
        if (locales.Count > 0)
            yield return Choice("@[Profile.Language]", $"{User}.Locale", locales.Select(l => (l, LocaleName(l))));
        var themes = ProfileTemplateBuilder.Themes(_serviceProvider);
        if (themes.Count > 0)
            yield return Choice("@[Profile.Theme]", $"{User}.Theme", themes.Select(t => (t, ProfileTemplateBuilder.ThemeLabel(t))));
    }

    // the slots' editors are the card's (XamlBuilder.SlotEditors); none without profile/metadata.json
    public Page Page(IReadOnlyList<UIElementBase> slots) => new()
    {
        Title = "@[Profile.Title]",
        Children = [
            new StackPanel()
            {
                Width = Length.FromString("40rem"),
                Gap = GapSize.FromString("1.5rem"),
                Children = [
                    Section("@[Profile.Personal]", ProfileTemplateBuilder.EditingPersonal,
                        ProfileTemplateBuilder.EditPersonal, ProfileTemplateBuilder.UpdatePersonal, validRequired: true, Personal()),
                    .. slots.Count == 0 ? Array.Empty<UIElementBase>() : [
                        Section("@[Profile.Defaults]", ProfileTemplateBuilder.EditingDefaults,
                            ProfileTemplateBuilder.EditDefaults, ProfileTemplateBuilder.UpdateDefaults, validRequired: false, slots)
                    ]
                ]
            }
        ]
    };
}
