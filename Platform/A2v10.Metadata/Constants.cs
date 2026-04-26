// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

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
        public const String Date = nameof(Date);
        public const String Done = nameof(Done);
    }
    public static class FieldSizes
    {
        public const Int32 Name = 255;
        public const Int32 Memo = 255;
    }
}
