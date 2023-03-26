// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Linq;


namespace A2v10.Xaml;


[ContentProperty("Children")]
public class Case : XamlElement
{
	public UIElementCollection Children { get; set; } = new();
	public String? Value { get; set; }

	public void RenderElement(RenderContext context)
	{
		var parentGrid = FindParent<Grid>();
		if (parentGrid != null)
		{
			foreach (var c in Children)
			{
				c.IsInGrid = true;
				using (context.GridContext(c, parentGrid))
				{
					c.RenderElement(context);
				}
			}
		}
		else
		{
			foreach (var c in Children)
				c.RenderElement(context);
		}
	}

	protected override void OnEndInit()
	{
		base.OnEndInit();
		foreach (var c in Children)
			c.SetParent(this);
	}

	public override void OnSetStyles(RootContainer root)
	{
		base.OnSetStyles(root);
		foreach (var c in Children)
			c.OnSetStyles(root);
	}
}

public class Else : Case
{
}

public class CaseCollection : List<Case>
{
	public CaseCollection()
	{
	}
}

[ContentProperty("Cases")]
public class Switch : UIElementBase
{
	public Object? Expression { get; set; }

	public CaseCollection Cases { get; set; } = new CaseCollection();

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		var expr = GetBinding(nameof(Expression)) 
			?? throw new XamlException("Binding 'Expression' must be a Bind");
        var cases = Cases.OrderBy(x => x is Else).ToList();

		for (var i = 0; i < cases.Count; i++)
		{
			var itm = cases[i];
			var t = new TagBuilder("template");
			var ifKey = (i == 0) ? "v-if " : "v-else-if";
			if (itm is Else)
			{
				t.MergeAttribute("v-else", String.Empty);
			}
			else
			{
				// convert values to string!
				t.MergeAttribute(ifKey, $"('' + {expr.GetPathFormat(context)}) === '{itm.Value}'");
			}
			t.RenderStart(context);
			itm.RenderElement(context);
			t.RenderEnd(context);
		}
	}

	protected override void OnEndInit()
	{
		base.OnEndInit();
		foreach (var c in Cases)
			c.SetParent(this);
	}

	public override void OnSetStyles(RootContainer root)
	{
		base.OnSetStyles(root);
		foreach (var ch in Cases)
			ch.OnSetStyles(root);
	}
}

