// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Globalization;

namespace A2v10.Metadata;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class IssueDateAttribute : Attribute
{
    public IssueDateAttribute(String date)
    {
        IssueDate = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
    public DateTime IssueDate { get; set; }
}
