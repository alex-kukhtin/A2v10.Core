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
        public const String IsFolder = nameof(IsFolder);
        public const String IsSystem = nameof(IsSystem);
        public const String RowNo = nameof(RowNo);
        public const String Owner = nameof(Owner);
        public const String Date = nameof(Date);
        public const String Done = nameof(Done);
        public const String RowVersion = "rv";
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

    public static class SchemaNames
    {
        public const String Catalog = "catalog";
        public const String Document = "document";
        public const String Journal = "journal";
        public const String Details = "details";
    }
}
