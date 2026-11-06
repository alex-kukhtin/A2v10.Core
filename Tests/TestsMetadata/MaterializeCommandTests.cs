// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A command the template runs by name is written as BindCmdExec: its name is positional there. Built
 * as BindCmd Execute { CommandName }, the page the runtime renders from the object worked, and the
 * materialized text lost the name - the page refused to open. XamlBeamTests reads the text back and
 * writes it again, so a name lost on the first write is lost on both and passes there.
 */
public class MaterializeCommandTests
{
    [Fact]
    public async Task Post_and_unpost_keep_their_names_in_the_text()
    {
        var mat = TestHost.GetService<EndpointMaterializer>();
        var text = (await mat.MaterializeAsync("/document/waybillin", "edit", MaterializeWhat.View)).Files[0].Text;

        Assert.Contains("{BindCmdExec post", text);
        Assert.Contains("{BindCmdExec unPost", text);
        Assert.DoesNotContain("{BindCmd Execute", text);
    }

    // the owner's page is not built (Constants.Actions.Show), so the list does not offer to open it
    [Fact]
    public async Task A_catalog_list_offers_no_show()
    {
        var mat = TestHost.GetService<EndpointMaterializer>();
        var text = (await mat.MaterializeAsync("/catalog/agent", "index", MaterializeWhat.View)).Files[0].Text;

        Assert.DoesNotContain("/catalog/agent/show", text);
    }
}
