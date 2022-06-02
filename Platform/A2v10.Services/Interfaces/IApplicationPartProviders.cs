// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Services;

public interface IModelJsonPartProvider
{
	Task<ModelJson?> GetModelJsonAsync(IPlatformUrl url);
}
