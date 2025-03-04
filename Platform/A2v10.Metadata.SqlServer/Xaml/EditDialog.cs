// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata.SqlServer;

internal partial class ModelPageBuilder
{
    UIElement CreateEditDialog(IPlatformUrl platformUrl, IModelView modelView, TableMetadata meta)
    {
        return new Dialog()
        {
            Title = $"@[{meta.Table.Singular()}]",
            Buttons = [
                new Button() {
                    Content = "@[SaveAndClose]",
                    Style = ButtonStyle.Primary,
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd("SaveAndClose"))
                },
                new Button() {
                    Content = "@[Cancel]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd("Close"))
                }
            ],
            Children = [
                new Grid(_xamlSericeProvider) {

                }
            ]
        };
    }
}
