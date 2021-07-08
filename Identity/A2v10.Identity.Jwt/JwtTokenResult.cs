// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.Jwt
{
	public record JwtTokenResponse
	{
		public Boolean success { get; set; }
		public String accessToken { get; set; }
		public String refreshToken { get; init; }
		public Int64 validTo { get; set; }
		public String user { get; set; }

		public JwtTokenResponse()
		{
			success = true;
		}
	}

	public record JwtTokenError 
	{
		public Boolean success => false;
		public String message { get; init; }
	}


	public class JwtTokenResult
	{
		public DateTime Expires { get; init; }
		public JwtTokenResponse Response  {get; init;}
	}
}