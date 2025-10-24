// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Xaml;

public class MultiSelect : ValuedControl, ITableControl
{
    public String? Placeholder { get; set; }
    public String? Url { get; set; }
    public Boolean Highlight { get; set; }
    public String? Data { get; set; }

    public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
    {
        if (CheckDisabledModel(context))
            return;
        var input = new TagBuilder("a2-multiselect", null, IsInGrid);
        onRender?.Invoke(input);

        MergeAttributes(input, context);
        MergeDisabled(input, context);
        MergeBindingAttributeString(input, context, "placeholder", nameof(Placeholder), Placeholder);
        MergeValue(input, context);


        input.MergeAttribute("url", Url);

        var fetchData = GetBinding(nameof(Data));
        if (fetchData != null)
            input.MergeAttribute(":fetch-data", fetchData.GetPath(context));
        else if (Data != null && Data.StartsWith('{'))
            input.MergeAttribute(":fetch-data", Data);

        input.RenderStart(context);
        RenderAddOns(context);
        input.RenderEnd(context);
    }
}
