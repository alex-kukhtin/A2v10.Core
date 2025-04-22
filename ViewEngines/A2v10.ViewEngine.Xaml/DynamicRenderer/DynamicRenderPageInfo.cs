
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Xaml.DynamicRendrer;

public struct DynamicRenderPageInfo
{
	public String RootId { get; set; }
	public IDataModel Model { get; init; }
	public UIElement Page { get; init; }
	public IPlatformUrl PlatformUrl { get; init; }
	public IModelView ModelView { get; init; }

	public String? Template { get; init; }
}
