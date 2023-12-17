using A2v10.Infrastructure;
using System;
using System.Dynamic;

namespace A2v10.Core.Web.Site.TestServices
{
	public class TestPageHandler(IViewEngineProvider _viewEngineProvider) : IEndpointHandler
	{
		public string RenderResult(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
		{
			var id = $"el{Guid.NewGuid()}";
			return
$$""""
<div class="page absolute page-grid" id="{{id}}"><a2-document-title page-title="PAGE HANDLER">
	</a2-document-title>
	TEXT FROM PAGE HANDLER {{modelView.EndpointHandler}}
	<br />
	Id = {{platformUrl.Id}}, X = {{prms.Get<Object>("X")}}, S = '{{prms.Get<String>("S")}}'
</div>
<script type="text/javascript">
(function() {
const DataModelController = component('baseController');
const vm = new DataModelController({
	el:'#{{id}}'
});
vm.__doInit__('$personnel/design/test/123/');
})();
</script>
"""";
		}
	}
}
