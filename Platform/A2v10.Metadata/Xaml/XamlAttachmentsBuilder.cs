// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

/* The attachments dialog: the list on the left, the selected one on the right. The selected row is a
 * Slot, not an If - nothing under it renders without a selection, and the fallback says why the place
 * is empty (design/blobs.md, Controls)
 */
internal class XamlAttachmentsBuilder
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    private const String Selected = $"{SqlBuilderAttachments.Collection}.$selected";

    public Dialog RenderIndexDialog()
    {
        Button command(String name, Icon icon, String? content, String? argument = null) => new()
        {
            Icon = icon,
            Content = content,
            Bindings = b =>
            {
                var cmd = new BindCmd(CommandType.Execute) { CommandName = name };
                if (argument != null)
                    cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(argument));
                b.SetBinding(nameof(Button.Command), cmd);
            }
        };

        var list = new Grid(_xamlServiceProvider)
        {
            Rows = RowDefinitions.FromString("Auto,1*"),
            Height = Length.FromString("100%"),
            AlignItems = AlignItem.Stretch,
            Children = [
                new Toolbar(_xamlServiceProvider)
                {
                    Children = [
                        command(AttachmentsTemplateBuilder.Upload, Icon.Upload, "@[Attachment.Upload]"),
                        command(AttachmentsTemplateBuilder.Remove, Icon.Delete, null, Selected)
                    ]
                },
                new List()
                {
                    AutoSelect = AutoSelectMode.FirstItem,
                    Bindings = b => b.SetBinding(nameof(List.ItemsSource), new Bind(SqlBuilderAttachments.Collection)),
                    Content = [
                        new ListItemSimple()
                        {
                            Bindings = b => b.SetBinding(nameof(ListItemSimple.Content), new Bind("Blob.Name"))
                        }
                    ]
                }
            ]
        };

        var preview = new Slot()
        {
            Bindings = b => b.SetBinding(nameof(Slot.Scope), new Bind(Selected)),
            Children = [
                new BlobPreview()
                {
                    Height = Length.FromString("100%"),
                    Bindings = b => b.SetBinding(nameof(BlobPreview.Value), new Bind("Blob"))
                }
            ],
            Fallback = new EmptyPanel()
            {
                Content = "@[Attachment.Empty]"
            }
        };

        return new Dialog()
        {
            Title = "@[Attachments]",
            Width = Length.FromString("60rem"),
            Height = Length.FromString("30rem"),
            Buttons = [
                new Button() {
                    Content = "@[Close]",
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Close))
                }
            ],
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Columns = ColumnDefinitions.FromString("20rem,1*"),
                    Rows = RowDefinitions.FromString("1*"),
                    Height = Length.FromString("100%"),
                    AlignItems = AlignItem.Stretch,
                    Children = [list, preview]
                }
            ]
        };
    }
}
