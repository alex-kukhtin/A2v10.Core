// Copyright © 2024-2025 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

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
}

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
}

public class BindCmdExec : BindCmd
{
    protected override String ClassName => nameof(BindCmdExec);
    public BindCmdExec(String path)
        : base()
    {
        Command = CommandType.Execute;
        CommandName = path;
    }
}
