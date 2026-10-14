// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* The script of /attachment/index. The names of the template commands are shared with
 * XamlAttachmentsBuilder, the name of the server's command with AttachmentEndpointBuilder - each
 * spelled once. The platform uploads and links (BlobService); what follows is the dialog's: only it
 * knows the shape of its row, so the result is appended here (design/blobs.md, $uploadBlob)
 */
internal static class AttachmentsTemplateBuilder
{
    internal const String Upload = "upload";
    internal const String Remove = "remove";
    internal const String DeleteCommand = "delete";

    private static String UploadUrl => $"/{Constants.SchemaNames.Attachment}/{AttachmentEndpointMetadata.UploadKey}";

    internal static String IndexTemplate => $$"""
        const template = {
            commands: {
                {{Upload}},
                {{Remove}}: {
                    exec: {{Remove}},
                    canExec: row => !!row,
                    confirm: `@[Confirm.Delete.Element]`
                }
            }
        };

        module.exports = template;

        // the owner and the base ride as data: exactly Id and Base, which the upload reads
        async function {{Upload}}() {
            const result = await this.$ctrl.$uploadBlob('{{UploadUrl}}', undefined, this.{{SqlBuilderAttachments.OwnerModel}});
            if (!result)
                return;
            const row = this.{{SqlBuilderAttachments.Collection}}.$append(result);
            row.$select();
        }

        // by base + owner + link, the pair the dialog was opened with
        async function {{Remove}}(row) {
            const owner = this.{{SqlBuilderAttachments.OwnerModel}};
            await this.$ctrl.$invoke('{{DeleteCommand}}', { Id: row.Id, {{Constants.FieldNames.BaseQuery}}: owner.{{Constants.FieldNames.BaseQuery}}, Owner: owner.Id });
            row.$remove();
        }
        """;
}
