// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Metadata;

namespace A2v10.Cli;

/* What the metadata layer refuses about ONE endpoint - the endpoint you just edited. The result
 * lists every check with a status, so "not checked" reads on the spot; a finding never travels
 * through JsonResult.Try, which means the tool could not run. See CLAUDE.md, "meta validate".
 */
internal sealed class ValidateCommand(IServiceProvider services)
{
    private readonly EndpointValidator _validator = services.GetRequiredService<EndpointValidator>();

    internal Command Build()
    {
        var cmd = new Command("validate",
            "Validate one endpoint: what it declares, the screens it renders and the print blanks it declares. " +
            "Which checks exist follows the endpoint's type and the result lists every one of them; they are " +
            "chained and stop at the first error. Nothing is written and the database is not read. " +
            "There is no application-wide form: validate the endpoint you just edited.");
        var endpointArg = new Argument<String>("endpoint")
        {
            Description = "Endpoint folder, $prefix for a module (e.g. catalog/agent)"
        };
        cmd.Arguments.Add(endpointArg);

        cmd.SetAction(r => JsonResult.Try(() => Validate(r.GetValue(endpointArg)!)));
        return cmd;
    }

    private async Task<Object> Validate(String endpoint)
    {
        MetadataSupport.Create(services).EnsureEnabled();

        return await _validator.ValidateAsync(endpoint);
    }
}
