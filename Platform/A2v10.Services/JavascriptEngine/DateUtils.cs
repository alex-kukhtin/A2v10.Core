// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Javascript;

public class DateUtils
{
#pragma warning disable IDE1006 // Naming Styles
    public static String format(Object? date, String format)
    {
        if (date == null)
            return String.Empty;
        if (date is DateTime dateTime)
            return dateTime.ToString(format);
        throw new InvalidOperationException($"DateUtils.format. Invalid date {date}");
    }
#pragma warning restore IDE1006 // Naming Styles
}
