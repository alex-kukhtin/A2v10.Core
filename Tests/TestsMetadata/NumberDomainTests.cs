// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;
using A2v10.Xaml;

namespace A2v10.Metadata.Tests;

/* IsNumber and IsCurrency exist so the display sites cannot drift apart. The theories pin who is in;
 * the facts walk every ColumnType, so a site that answers by its own list fails here.
 */
public class NumberDomainTests
{
    public static TheoryData<ColumnType> AllTypes => [.. Enum.GetValues<ColumnType>()];

    [Theory]
    [InlineData(ColumnType.Amount)]
    [InlineData(ColumnType.Price)]
    [InlineData(ColumnType.Money)]
    public void Money(ColumnType type)
    {
        Assert.True(type.IsCurrency());
        Assert.True(type.IsNumber());
    }

    [Theory]
    [InlineData(ColumnType.Qty)]
    [InlineData(ColumnType.Percent)]
    [InlineData(ColumnType.Factor)]
    [InlineData(ColumnType.Integer)]
    [InlineData(ColumnType.Decimal)]
    [InlineData(ColumnType.Float)]
    public void A_number_that_is_not_money(ColumnType type)
    {
        Assert.True(type.IsNumber());
        Assert.False(type.IsCurrency());
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Money_is_a_number(ColumnType type) =>
        Assert.True(!type.IsCurrency() || type.IsNumber());

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void A_number_is_right_aligned_and_a_number_column(ColumnType type)
    {
        if (type == ColumnType.RowNumber)
            return; // aligned right, but a row's ordinal and not a value
        Assert.Equal(type.IsNumber(), type.ToXamlAlign() == TextAlign.Right);
        Assert.Equal(type.IsNumber(), type.ToXamlColumnRole() == ColumnRole.Number);
    }

    [Theory]
    [InlineData(ColumnType.Amount, DataType.Currency)]
    [InlineData(ColumnType.Price, DataType.Currency)]
    [InlineData(ColumnType.Money, DataType.Currency)]
    [InlineData(ColumnType.Qty, DataType.Number)]
    [InlineData(ColumnType.Factor, DataType.Number)]
    [InlineData(ColumnType.Integer, DataType.Number)]
    [InlineData(ColumnType.Percent, DataType.Percent)]
    public void The_format_of_a_number(ColumnType type, DataType expected) =>
        Assert.Equal(expected, type.ToXamlDataType());
}
