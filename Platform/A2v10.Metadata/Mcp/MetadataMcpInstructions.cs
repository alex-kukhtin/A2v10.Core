// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* mcp-instructions.txt in the application root, beside mcp.json: plain text, as the client hands it to the
 * model. A file and not a key of mcp.json - JSON has no multi-line literal. Read on every request to /mcp,
 * no cache: a few KB beside the SQL a tool runs. Through IAppCodeProvider, so a compiled application
 * reads it out of its container. No file - no instructions.
 */
public sealed class MetadataMcpInstructions(IAppCodeProvider codeProvider) : IPlatformMcpInstructions
{
    internal const String FileName = "mcp-instructions.txt";

    public async Task<String?> GetInstructionsAsync()
    {
        using var stream = codeProvider.FileStreamRO(FileName, primaryOnly: true);
        if (stream == null)
            return null;
        using var sr = new StreamReader(stream);
        return await sr.ReadToEndAsync();
    }
}
