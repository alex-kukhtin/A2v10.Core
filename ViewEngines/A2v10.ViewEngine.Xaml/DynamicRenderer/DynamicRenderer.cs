// Copyright © 2021-2025 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Xaml.DynamicRendrer;

public class DynamicRenderer(IServiceProvider serviceProvider)
{
	private readonly IDataScripter _dataScripter = serviceProvider.GetRequiredService<IDataScripter>();
	private readonly IXamlPartProvider _partProvider = serviceProvider.GetRequiredService<IXamlPartProvider>();
	private readonly ILocalizer _localizer = serviceProvider.GetRequiredService<ILocalizer>();

	public void InitPage(UIElement page)
	{
        if (page is IInitComplete initComplete)
        {
            initComplete.InitComplete();
        }

        var stylesPart = _partProvider.GetCachedXamlPartOrNull("styles.xaml");
        if (stylesPart != null)
        {
            if (stylesPart is not Styles styles)
                throw new InvalidOperationException("Xaml. Styles is not 'Styles'");
            if (page is IRootContainer root)
            {
                root.SetStyles(styles);
            }
        }
    }

    public async Task<String> RenderPage(DynamicRenderPageInfo info)
	{
        InitPage(info.Page);

		using var stringWriter = new StringWriter();

		var ri = new RenderInfo()
		{
			RootId = info.RootId,
			DataModel = info.Model
		};
		var ctx = new RenderContext(info.Page, ri, _localizer, stringWriter)
		{
			RootId = info.RootId,
			Path = info.PlatformUrl.BaseUrl
		};
		info.Page.RenderElement(ctx);

		var msi = new ModelScriptInfo()
		{
			RootId = info.RootId,
			BaseUrl = info.PlatformUrl.BaseUrl,
			DataModel = info.Model,
			IsDialog = info.ModelView.IsDialog,
			IsIndex = info.ModelView.IsIndex,
			IsPlain = info.ModelView.IsPlain,
			IsSkipDataStack = info.ModelView.IsSkipDataStack,
			Template = info.Template,
			Path = "@Model.Template"
		};
		var si = await _dataScripter.GetModelScript(msi);

		stringWriter.Write(si.Script);

		return stringWriter.ToString();
	}
}
