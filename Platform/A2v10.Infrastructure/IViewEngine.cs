// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure
{

    public interface IRenderInfo
    {
        String? RootId { get; }
        String? FileName { get; }
        String? FileTitle { get; }
        String? Path { get; }
        // String? Text { get; }
        IDataModel? DataModel { get; }
        //public ITypeChecker TypeChecker;
        String? CurrentLocale { get; }
        Boolean SecondPhase { get; }
        Boolean Admin { get; }
    }

    public interface IRenderResult
    {
        String Body { get; }
        String ContentType { get; }
    }

    public interface IViewEngine
    {
        Task<IRenderResult> RenderAsync(IRenderInfo renderInfo);
    }
}
