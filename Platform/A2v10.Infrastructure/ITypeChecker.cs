// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure
{
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
}
