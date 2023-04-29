// Copyright © 2019-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public enum TypeCheckerTypeCode
{
	String,
	Date,
	Boolean,
	Skip
}

public interface ITypeChecker
{
	void CreateChecker(String fileName, IDataModel model);
	void CheckXamlExpression(String expression);
	void CheckTypedXamlExpression(String expression, TypeCheckerTypeCode type);
}
