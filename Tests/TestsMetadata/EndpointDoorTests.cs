// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;
using System.Reflection;

using A2v10.Infrastructure;
using A2v10.Services.Api;

namespace A2v10.Metadata.Tests;

/* The door EndpointDataService opens a record by (DataAt.DoorAsync): the caller's; else the one the generated
 * client of the metadata layer opens; else catalog - dialog, document - page; else refused. The data service is
 * a recorder: what matters is the address it is asked, not what it answers.
 */
public class EndpointDoorTests
{
    sealed class Asked(Object?[] args) : Exception
    {
        public Object?[] Args => args;
    }

    // public and not sealed: DispatchProxy derives from it
    public class Recorder : DispatchProxy
    {
        protected override Object? Invoke(MethodInfo? method, Object?[]? args) => throw new Asked(args!);
    }

    static DataAt At(String route, IAppRuntimeBuilder runtime, UrlKind? edit = null) =>
        new EndpointDataService(DispatchProxy.Create<IDataService, Recorder>(), runtime).At(route, edit: edit);

    static DataAt At(String route) => At(route, TestHost.GetService<IAppRuntimeBuilder>());

    static async Task<Object?> DoorOfLoad(DataAt door) => (await Assert.ThrowsAsync<Asked>(() => door.LoadAsync("1"))).Args[0];

    [Theory]
    [InlineData("sale/invoice", UrlKind.Page)]    // an alias of 'document': only the metadata layer knows it
    [InlineData("catalog/project", UrlKind.Page)] // 'forms.edit.is: page' - not the default of a catalog
    [InlineData("catalog/agent", UrlKind.Dialog)]
    public async Task A_metadata_endpoint_opens_by_the_door_of_its_generated_client(String route, UrlKind door)
    {
        Assert.Equal(door, await DoorOfLoad(At(route)));
    }

    [Fact]
    public async Task The_save_goes_by_the_same_door()
    {
        var asked = await Assert.ThrowsAsync<Asked>(() => At("sale/invoice").SaveAsync(new ExpandoObject()));
        Assert.Equal("_page/sale/invoice/edit/0", asked.Args[0]);
    }

    // a classic host: nothing to ask, the default of the kind
    [Fact]
    public async Task Without_metadata_a_catalog_is_a_dialog_and_a_document_a_page()
    {
        Assert.Equal(UrlKind.Dialog, await DoorOfLoad(At("catalog/agent", new NullAppRuntimeBuilder())));
        Assert.Equal(UrlKind.Page, await DoorOfLoad(At("document/waybill", new NullAppRuntimeBuilder())));
    }

    [Fact]
    public async Task Without_metadata_and_a_kind_the_door_is_the_callers()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => At("sales/invoice", new NullAppRuntimeBuilder()).LoadAsync("1"));
        Assert.StartsWith("'sales/invoice': no metadata.json opens a record here, and 'sales' is neither 'catalog' nor 'document'", ex.Message);

        Assert.Equal(UrlKind.Dialog, await DoorOfLoad(At("sales/invoice", new NullAppRuntimeBuilder(), UrlKind.Dialog)));
    }
}
