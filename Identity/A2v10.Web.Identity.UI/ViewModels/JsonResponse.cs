// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Web.Identity.UI;

public class JsonResponse
{
	public Boolean Success { get; init; }
	public String? Message { get; init; }

    public static JsonResponse Error(String message)
	{
		return new JsonResponse { Success = false, Message = message };
	}
	public static JsonResponse Error(Exception ex)
	{
		return new JsonResponse { Success = false, Message = ex.Message };
	}
	public static JsonResponse Ok(String message)
	{
		return new JsonResponse { Success = true, Message = message };
	}
}

