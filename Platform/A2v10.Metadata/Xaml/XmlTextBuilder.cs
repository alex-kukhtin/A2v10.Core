// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal class XmlTextBuilder
{
    private Form _form { get; init; } = default!;
    private XNamespace _ns = "clr-namespace:A2v10.Xaml;assembly=A2v10.Xaml";
    public static String Build(FormMetadata form)
    {
        var builder = new XmlTextBuilder()
        {
            _form = form.form
        };
        var xdoc = builder.BuildText();
        return xdoc.ToString();
    }

    XElement CreateElement(FormItem item)
    {
        return new XElement(_ns + item.Is.ToString(), ChildItems(item), Attributes(item));
    }
    IEnumerable<XAttribute> DataGridAttributes(FormItem item)
    {
        yield return new XAttribute("FixedHeader", "True");
        yield return new XAttribute("ItemsSource", item.BindText());
    }

    IEnumerable<XAttribute> PagerAttributes(FormItem item)
    {
        yield return new XAttribute(nameof(Pager.Source), item.BindText());
    }

    IEnumerable<XAttribute> DataGridColumnAttributes(FormItem item)
    {
        yield return new XAttribute("Content", item.BindText());

        var header = item.Label.Localize();
        if (!String.IsNullOrEmpty(header))
            yield return new XAttribute("Header", header);

        var props = item.Props;
        if (props == null)
            yield break;
        if (props.Fit)
            yield return new XAttribute("Fit", true);
        if (props.NoWrap)
            yield return new XAttribute("NoWrap", true);
        if (props.LineClamp > 0)
            yield return new XAttribute("LineClamp", props.LineClamp);
    }
    IEnumerable<XAttribute> ButtonAttributes(FormItem item)
    {
        var content = item.Label.Localize();
        var icon = item.Command2Icon();
        if (!String.IsNullOrEmpty(content))
            yield return new XAttribute("Content", content);
        if (icon != Icon.NoIcon)
            yield return new XAttribute("Icon", icon.ToString());
    }

    IEnumerable<XAttribute> Attributes(FormItem item)
    {
        if (item.Grid != null)
        {
            if (item.Grid.Row > 0)
                yield return new XAttribute("Grid.Row", item.Grid.Row);
            if (item.Grid.Col > 0)
                yield return new XAttribute("Grid.Col", item.Grid.Col);
            if (item.Grid.RowSpan > 0)
                yield return new XAttribute("Grid.RowSpan", item.Grid.RowSpan);
            if (item.Grid.ColSpan > 0)
                yield return new XAttribute("Grid.ColSpan", item.Grid.ColSpan);
        }
        var attrs = item.Is switch {
            FormItemIs.DataGridColumn => DataGridColumnAttributes(item),
            FormItemIs.DataGrid => DataGridAttributes(item),
            FormItemIs.Pager => PagerAttributes(item),
            FormItemIs.Button => ButtonAttributes(item),
            _ => Enumerable.Empty<XAttribute>()
        };
        foreach (var attr in attrs)
            yield return attr;
    }

    IEnumerable<Object> ChildItems(FormItem item)
    {
        if (item.Items == null)
            return Enumerable.Empty<XElement>();
        return item.Items.Select(CreateElement);
    }
    XElement CreateCollectionView()
    {
        return new XElement(_ns + $"{_form.Is}.CollectionView");
    }
    IEnumerable<Object> FormItems()
    {
        if (_form.UseCollectionView)
            yield return CreateCollectionView();
        if (_form.Items != null)
            foreach (var itm in _form.Items)
                yield return CreateElement(itm);
    }

    XDocument BuildText()
    {
        return new XDocument(
            new XElement(_ns + _form.Is.ToString(), FormItems(), Attributes(_form))
        );
    }
}
