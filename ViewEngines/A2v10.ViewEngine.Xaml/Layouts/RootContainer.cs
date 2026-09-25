// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.


using System.Collections.Generic;

namespace A2v10.Xaml;

[WrapContent]
public sealed class ResourceDictionary : Dictionary<String, Object>
{
}

public abstract class RootContainer : Container, IUriContext, IRootContainer
{
	#region IUriContext
	public Uri? BaseUri { get; set; }
	#endregion


	#region IRootContainer
	/* Once per styles object: a cached root is rendered many times with the same one. By reference and
	 * not by a flag - an edited styles.xaml (Watch) arrives as a new object, and the root takes it.
	 */
	public void SetStyles(Styles styles)
	{
		if (ReferenceEquals(Styles, styles))
			return;
		Styles = styles;
		OnSetStyles(this);
		foreach (var c in Components)
			c.Value.OnSetStyles(this);
	}

	#endregion

	/* Once per root. A page may be built once and rendered many times (a cached form), and the
	 * renderer initializes whatever it is given - so the second call is the one that does nothing.
	 * No lock: a root is shared only after its owner has initialized it (see the metadata form cache).
	 */
	private Boolean _initComplete;
	public override void InitComplete()
	{
		if (_initComplete)
			return;
		base.InitComplete();
		_initComplete = true;
	}

	protected ResourceDictionary? _resources;

	public ResourceDictionary Resources
	{
		get
		{
			_resources ??= [];
			return _resources;
		}
		set
		{
			_resources = value;
		}
	}
	public AccelCommandCollection AccelCommands { get; set; } = [];


	public ComponentDictionary Components { get; set; } = [];

	public XamlElement? FindComponent(String name)
	{
		if (Components.TryGetValue(name, out XamlElement? comp))
			return comp;
		return null;
	}

	public Object? FindResource(String key)
	{
		if (_resources == null)
			return null;
		if (_resources.TryGetValue(key, out Object? resrc))
			return resrc;
		return null;
	}

	internal Styles? Styles { get; set; }

	protected virtual void RenderAccelCommands(RenderContext context)
	{
		if (AccelCommands == null || AccelCommands.Count == 0)
			return;
		var cmd = new TagBuilder("template");
		cmd.RenderStart(context);
		foreach (var ac in AccelCommands)
			ac.RenderElement(context);
		cmd.RenderEnd(context);
	}

	private readonly List<Action> _contextMenus = [];
	public void RegisterContextMenu(Action action)
	{
		_contextMenus.Add(action);
	}

	public void RenderContextMenus()
	{
		foreach (var a in _contextMenus)
			a();
	}
}

