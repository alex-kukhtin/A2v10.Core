using System;
using A2v10.Infrastructure;

using A2v10.System.Xaml;

namespace A2v10.Xaml
{
	public class XamlRenderer : IRenderer
	{
		private readonly IProfiler _profile;
		private readonly IAppCodeProvider _codeprovider;
		private readonly IXamlReaderService _xamlReader;


		public XamlRenderer(IProfiler profile, IAppCodeProvider provider, IXamlReaderService xamlReader)
		{
			_profile = profile;
			_codeprovider = provider;
			_xamlReader = xamlReader;
		}

		public void Render(RenderInfo info)
		{
			if (String.IsNullOrEmpty(info.FileName))
				throw new XamlException("No source for render");
			IProfileRequest request = _profile.CurrentRequest;
			String fileName = String.Empty;
			UIElementBase uiElem = null;

			var xamlServiceOptions = new XamlServicesOptions()
			{
				OnCreateReader = (rdr) =>
				{
					rdr.InjectService<IAppCodeProvider>(_codeprovider);
				}
			};

			using (request.Start(ProfileAction.Render, $"load: {info.FileTitle}"))
			{
				// XamlServices.Load sets IUriContext
				if (!String.IsNullOrEmpty(info.FileName))
				{
					using var fileStream = _codeprovider.FileStreamFullPathRO(info.FileName);
					uiElem = _xamlReader.Load(fileStream, new Uri(info.FileName)) as UIElementBase;
				}
				else if (!String.IsNullOrEmpty(info.Text))
					uiElem = _xamlReader.ParseXml(info.Text) as UIElementBase;
				else
					throw new XamlException("Xaml. There must be either a 'FileName' or a 'Text' property");
				if (uiElem == null)
					throw new XamlException("Xaml. Root is not 'UIElement'");

				var stylesPath = _codeprovider.MakeFullPath(String.Empty, "styles.xaml");
				if (_codeprovider.FileExists(stylesPath))
				{
					var stylesStream = _codeprovider.FileStreamFullPathRO(stylesPath);
					if (stylesStream != null)
					{
						if (_xamlReader.Load(stylesStream, new Uri(stylesPath)) is not Styles styles)
							throw new XamlException("Xaml. Styles is not 'Styles'");
						if (uiElem is RootContainer root)
						{
							root.Styles = styles;
							root?.OnSetStyles();
						}
					}
				}
			}

			using (request.Start(ProfileAction.Render, $"render: {info.FileTitle}"))
			{
				RenderContext ctx = new(uiElem, info)
				{
					RootId = info.RootId,
					Path = info.Path
				};

				if (info.SecondPhase)
				{
					if (uiElem is not ISupportTwoPhaseRendering twoPhaseRender)
						throw new XamlException("The two-phase rendering is not available");
					twoPhaseRender.RenderSecondPhase(ctx);
				}
				else
				{
					uiElem.RenderElement(ctx);
				}
			}

			if (uiElem is IDisposable disp)
			{
				disp.Dispose();
			}
		}
	}
}
