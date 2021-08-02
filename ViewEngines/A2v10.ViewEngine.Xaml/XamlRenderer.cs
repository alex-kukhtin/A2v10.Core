// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.IO;
using A2v10.Infrastructure;
using A2v10.System.Xaml;

namespace A2v10.Xaml
{
	public class XamlRenderer : IRenderer
	{
		private readonly IProfiler _profile;
		private readonly IAppCodeProvider _codeprovider;
		private readonly IXamlReaderService _xamlReader;
		private readonly ILocalizer _localizer;

		public XamlRenderer(IProfiler profile, IAppCodeProvider provider, IXamlReaderService xamlReader, ILocalizer localizer)
		{
			_profile = profile;
			_codeprovider = provider;
			_xamlReader = xamlReader;
			_localizer = localizer;
		}

		public void Render(IRenderInfo info, TextWriter writer)
		{
			if (String.IsNullOrEmpty(info.FileName))
				throw new XamlException("No source for render");
			IProfileRequest request = _profile.CurrentRequest;
			String fileName = String.Empty;
			IXamlElement uiElem = null;

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
					uiElem = _xamlReader.Load(fileStream, new Uri(info.FileName)) as IXamlElement;
				}
				else if (!String.IsNullOrEmpty(info.Text))
					uiElem = _xamlReader.ParseXml(info.Text) as IXamlElement;
				else
					throw new XamlException("Xaml. There must be either a 'FileName' or a 'Text' property");
				if (uiElem == null)
					throw new XamlException("Xaml. Root is not 'IXamlElement'");

				// TODO - StylesXaml cache
				var stylesPath = _codeprovider.MakeFullPath(String.Empty, "styles.xaml", info.Admin);
				if (_codeprovider.FileExists(stylesPath))
				{
					using var stylesStream = _codeprovider.FileStreamFullPathRO(stylesPath);
					if (_xamlReader.Load(stylesStream, new Uri(stylesPath)) is not Styles styles)
						throw new XamlException("Xaml. Styles is not 'Styles'");
					if (uiElem is IRootContainer root)
					{
						root.SetStyles(styles);
					}
				}
			}

			using (request.Start(ProfileAction.Render, $"render: {info.FileTitle}"))
			{
				RenderContext ctx = new(uiElem, info, _localizer, writer)
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
