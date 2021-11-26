// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.


using System;
using System.Text;
using A2v10.System.Xaml;

namespace A2v10.Xaml
{
	[AttachedProperties("Fill,Skip")]
	public class FullHeightPanel : Container
	{
		private readonly IAttachedPropertyManager _attachedPropertyManager;

		public FullHeightPanel(IServiceProvider serviceProvider)
		{
			_attachedPropertyManager = serviceProvider.GetService(typeof(IAttachedPropertyManager)) as IAttachedPropertyManager;
		}

		#region Attached Properties


		public Boolean? GetFill(Object obj)
		{
			return _attachedPropertyManager.GetProperty<Boolean?>("FullHeightPanel.Fill", obj);
		}

		public Boolean? GetSkip(Object obj)
		{
			return _attachedPropertyManager.GetProperty<Boolean?>("FullHeightPanel.Skip", obj);
		}

		#endregion

		public Length MinWidth { get; set; }

		String GetRows()
		{
			var sb = new StringBuilder(); 
			foreach (var c in Children)
			{
				var skip = GetSkip(c);
				if (skip.HasValue && skip.Value)
					continue;
				var fill = GetFill(c);
				if (fill.HasValue && fill.Value)
					sb.Append("1fr ");
				else
					sb.Append("auto ");
			}
			return sb.ToString();
		}

		public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
		{
			if (SkipRender(context))
				return;
			var panel = new TagBuilder("div", "full-height-panel", IsInGrid);
			panel.MergeAttribute("key", Guid.NewGuid().ToString()); // disable vue reusing
			MergeAttributes(panel, context);
			if (MinWidth != null)
				panel.MergeStyleUnit("min-width", MinWidth.Value);
			panel.MergeStyle("grid-template-rows", GetRows());
			panel.RenderStart(context);
			RenderChildren(context);
			panel.RenderEnd(context);
		}
	}
}
