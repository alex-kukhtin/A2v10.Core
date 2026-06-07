// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Xaml;

public enum FlowWrap
{
    Scroll,
    Wrap
    // ReOrient - TBD
}

public enum FlowBorderStyle
{
    None,
    Light,
    Normal
}

public enum FlowBehavior 
{ 
    Static, 
    Collapsible, 
    Popup 
}

[AttachedProperties("Grow,AlignSelf")]
public class FlowPanel(IServiceProvider serviceProvider) : Container
{
    public Orientation Orientation { get; init; }
    public FlowWrap WrapMode { get; init; }
    public GapSize? Gap { get; init; }
    public FlowBorderStyle Border {  get; init; }
    public AlignItems AlignItems { get; init; }
    public JustifyItems JustifyItems { get; init; }
    public Object? Header { get; init; }
    public FlowBehavior Behavior { get; init; }
    public Boolean? Collapsed { get; init; }

    private readonly IAttachedPropertyManager _attachedPropertyManager = serviceProvider.GetRequiredService<IAttachedPropertyManager>();

    #region ISupportAttached
    public IAttachedPropertyManager AttachedPropertyManager => _attachedPropertyManager;
    #endregion

    #region Attached Properties
    public Int32? GetGrow(Object obj)
    {
        return _attachedPropertyManager.GetProperty<Int32?>("FlexPanel.Grow", obj);
    }
    public void SetGrow(Object obj, Int32 grow)
    {
        _attachedPropertyManager.SetProperty("FlexPanel.Grow", obj, grow);
    }
    #endregion
    public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
    {
        if (SkipRender(context))
            return;
        var flow = new TagBuilder("div", "flow-panel", IsInGrid);
        onRender?.Invoke(flow);
        MergeAttributes(flow, context);
        if (AlignItems != AlignItems.Default)
            flow.AddCssClass("align-" + AlignItems.ToString().ToLowerInvariant());
        if (JustifyItems != JustifyItems.Default)
            flow.AddCssClass("justify-" + JustifyItems.ToString().ToKebabCase());
        if (Gap != null)
            flow.MergeStyle("gap", Gap.ToString());
        flow.RenderStart(context);
        RenderChildren(context);
        flow.RenderEnd(context);
    }

    protected override void OnEndInit()
    {
        base.OnEndInit();
        if (Header is XamlElement hElem)
            hElem?.SetParent(this);
    }
}
