// Copyright © 2024-2025 Oleksandr Kukhtin. All rights reserved.

using System.Text;

namespace A2v10.Xaml;

[IgnoreWriteProperties("DataType,HideZeros,NegativeRed")]
public class BindSum : Bind
{
    protected override String ClassName => nameof(BindSum);
    public BindSum(String path) 
        :base(path)
    { 
        DataType = DataType.Currency;
        HideZeros = true;
        NegativeRed = true;
    }
    public override String CreateMarkup()
    {
        var sw = new StringBuilder("{");
        sw.Append(ClassName);
        if (!String.IsNullOrEmpty(Path))
            sw.Append($" {Path}");
        sw.Append('}');
        return sw.ToString();
    }
}

[IgnoreWriteProperties("DataType,HideZeros,NegativeRed")]
public class BindNumber : Bind
{
    protected override String ClassName => nameof(BindNumber);
    public BindNumber(String path)
        : base(path)
    {
        DataType = DataType.Number;
        HideZeros = true;
        NegativeRed = true;
    }
    public override String CreateMarkup()
    {
        var sw = new StringBuilder("{");
        sw.Append(ClassName);
        if (!String.IsNullOrEmpty(Path))
            sw.Append($" {Path}");
        sw.Append('}');
        return sw.ToString();
    }
}

[IgnoreWriteProperties("CommandName")]
public class BindCmdExec : BindCmd
{
    protected override String ClassName => nameof(BindCmdExec);
    public BindCmdExec(String path)
        : base()
    {
        Command = CommandType.Execute;
        CommandName = path;
    }
    public override String CreateMarkup()
    {
        var sw = new StringBuilder("{");
        sw.Append(ClassName);
        if (!String.IsNullOrEmpty(CommandName))
            sw.Append($" {CommandName}");
        AddXtraMarkup(sw);
        sw.Append('}');
        return sw.ToString();
    }
}
