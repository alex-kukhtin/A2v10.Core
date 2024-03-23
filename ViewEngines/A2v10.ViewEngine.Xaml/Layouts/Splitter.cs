// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Xaml;


[AttachedProperties("Width,MinWidth")]
public class Splitter(IServiceProvider serviceProvider) : Container
{
	public Orientation Orientation { get; set; }
	public Length? Height { get; set; }
	public Length? MinWidth { get; set; }

	private readonly IAttachedPropertyManager _attachedPropertyManager = serviceProvider.GetRequiredService<IAttachedPropertyManager>();

    #region Attached Properties

    public GridLength GetWidth(Object obj)
	{
		var prop = _attachedPropertyManager.GetProperty<Object>("Splitter.Width", obj);
		if (prop == null)
			return new GridLength();
		return GridLength.FromString(prop.ToString()!);
	}


	public Length GetMinWidth(Object obj)
	{
		var prop = _attachedPropertyManager.GetProperty<Object>("Splitter.MinWidth", obj);
		if (prop == null)
			return new Length();
		return Length.FromString(prop.ToString()!);
	}

	#endregion

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		/* TODO: 
             * 1. Horizontal splitter
            */
		if (SkipRender(context))
			return;
		var spl = new TagBuilder("div", "splitter");
		onRender?.Invoke(spl);
		spl.MergeAttribute("key", Guid.NewGuid().ToString()); // disable vue reusing
		MergeAttributes(spl, context);
		if (Height != null)
			spl.MergeStyle("height", Height.Value);
		if (MinWidth != null)
			spl.MergeStyle("min-width", MinWidth.Value);
		spl.AddCssClass(Orientation.ToString().ToLowerInvariant());
		// width
		GridLength p1w = GetWidth(Children[0]) ?? GridLength.Fr1();
		GridLength p2w = GetWidth(Children[1]) ?? GridLength.Fr1();

		String rowsCols = Orientation == Orientation.Vertical ? "grid-template-columns" : "grid-template-rows";
		spl.MergeStyle(rowsCols, $"{p1w} 5px {p2w}");

		spl.RenderStart(context);

		// first part
		var p1 = new TagBuilder("div", "spl-part spl-first");
		p1.RenderStart(context);
		Children[0].RenderElement(context);
		p1.RenderEnd(context);

		new TagBuilder("div", "spl-handle")
			.MergeAttribute(Orientation == Orientation.Vertical ? "v-resize" : "h-resize", String.Empty)
			//.MergeAttribute("key", Guid.NewGuid().ToString()) // disable vue reusing
			.MergeAttribute("first-pane-width", p1w?.Value?.ToString())
			.MergeAttribute("data-min-width", GetMinWidth(Children[0])?.Value?.ToString())
			.MergeAttribute("second-min-width", GetMinWidth(Children[1])?.Value?.ToString())
			.Render(context);

		// second part
		var p2 = new TagBuilder("div", "spl-part spl-second");
		p2.RenderStart(context);
		Children[1].RenderElement(context);
		p2.RenderEnd(context);

		// drag-handle
		new TagBuilder("div", "drag-handle")
			.Render(context);

		spl.RenderEnd(context);
	}

	protected override void OnEndInit()
	{
		base.OnEndInit();
		if (Children.Count != 2)
			throw new XamlException("The splitter must have two panels");
		if (Orientation == Orientation.Horizontal)
			throw new XamlException("The horizontal splitter is not yet supported");
		EndInitAttached(_attachedPropertyManager);
	}
}
