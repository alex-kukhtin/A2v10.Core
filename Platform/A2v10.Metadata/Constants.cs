// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class Constants
{
    public const Int32 MultilineThreshold = 200;

    public static class FieldNames
    {
        public const String Id = nameof(Id);
        public const String Name = nameof(Name);
        public const String Memo = nameof(Memo);
        public const String Void = nameof(Void);
        public const String IsSystem = nameof(IsSystem);
        public const String RowNo = nameof(RowNo);
        public const String Owner = nameof(Owner);
        public const String Date = nameof(Date);
        public const String Done = nameof(Done);
        public const String Document = nameof(Document);
        public const String RowVersion = "rv";
        public const String Parent = nameof(Parent);
        public const String Folder = nameof(Folder);
        public const String Color = nameof(Color);
        public const String For = nameof(For);
        public const String Tag = nameof(Tag);
        public const String Tags = nameof(Tags);
    }
    public static class FieldSizes
    {
        public const Int32 Name = 255;
        public const Int32 Memo = 255;
    }

    public static class FormNames
    {
        public const String Index  = "index";
        public const String Edit   = "edit";
        public const String Open   = "open";
        public const String Show   = "show";
        public const String Browse = "browse";
    }

    public static class SqlNames
    {
        /* A list of ids and nothing else, on the real 'platformid' - which is why it is ours and
         * not a2sys.[Id.TableType]: that one is declared 'bigint', and a database whose platformid
         * is uniqueidentifier would take the rows and write none. Lives beside 'platformid' itself.
         */
        public const String IdTableType = "dbo.[PlatformId.TableType]";
    }

    /* Platform entries of the filter namespace - the ones a trait or the kind contributes, as
     * opposed to the ones a column contributes under its own name. See FilterMetadata.
     */
    public static class FilterNames
    {
        public const String Period = nameof(Period);
        public const String Tags = nameof(Tags);
    }

    public static class SchemaNames
    {
        public const String Catalog = "catalog";
        public const String Document = "document";
        public const String Journal = "journal";
        public const String Details = "details";
        public const String Report = "report";
        /* Platform-owned namespaces: their endpoints are declared in code, not in files. Own
         * namespaces on purpose - inside catalog/ or document/ they would be shadowed by, or
         * confused with, an application endpoint of the same name.
         */
        public const String Operation = "operation";
        public const String Tag = "tag";
    }
}
