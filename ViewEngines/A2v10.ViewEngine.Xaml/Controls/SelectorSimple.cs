﻿// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.ViewEngine.Xaml;

namespace A2v10.Xaml;

public class SelectorSimple : Selector
{

	public String? Url { get; set; }
	public String? Data { get; set; }
    public Boolean Folder { get; set; }

    public Action<RenderContext>? _renderAction = null;

    protected override void OnEndInit()
	{
		base.OnEndInit();

        String? urlData = null;
        (Url, urlData) = Url.ParseUrlQuery();
        if (urlData != null)
            Data = urlData;

		if (AddOns.Count > 0)
		{
			for (int i = 0; i < AddOns.Count; i++)
			{
				var command = AddOns[i].GetBindingCommand(nameof(Command));
				if (command != null)
				{
					if (command.Command == CommandType.Browse && command.Url == $"{Url}/browse")
					{
						_renderAction = null;
						return;
					}
				}
            }
        }

        if (String.IsNullOrEmpty(DisplayProperty))
            DisplayProperty = "Name";

        var suffix = Folder ? "folder" : String.Empty;

        var urlBind = GetBinding(nameof(Url));
        var cmd = new BindCmd()
        {
            Command = CommandType.Browse,
        };
        if (urlBind != null)
        {
            _renderAction = (ctx) =>
            {
                this.BindImpl.SetBinding(nameof(Fetch), new Bind() { Path = $"{urlBind.GetPathFormat(ctx)} + '/fetch{suffix}'" });
                cmd.BindImpl.SetBinding(nameof(Url), new Bind() { Path = $"{urlBind.GetPathFormat(ctx)} + '/browse{suffix}'" });
            };
        }
        else
        {
            Fetch = $"{Url}/fetch{suffix}";
            cmd.Url = $"{Url}/browse{suffix}";
        }
        var hlink = new Hyperlink()
        {
            Icon = Icon.Search,
        };
        var val = this.GetBinding(nameof(Value));
        if (val != null)
            cmd.BindImpl.SetBinding("Argument", new Bind() { Path = val.Path });
        var dat = this.GetBinding("Data");
        if (dat != null)
        {
            cmd.BindImpl.SetBinding("Data", dat);
            this.BindImpl.SetBinding(nameof(FetchData), dat);
        }
        else if (Data != null)
        {
            cmd.Data = Data;
            this.FetchData = Data;
        }
        hlink.BindImpl.SetBinding("Command", cmd);
        AddOns.Insert(0, hlink);
	}

    public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
    {
        _renderAction?.Invoke(context);
        base.RenderElement(context, onRender);
    }

}
