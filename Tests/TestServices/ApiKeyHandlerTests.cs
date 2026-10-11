// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using A2v10.Identity.Core;
using A2v10.Web.Identity;
using A2v10.Web.Identity.ApiKey;

namespace TestServices;

/* A key that does not hold gets one refusal, whatever broke: two answers on AES-CBC are a padding
 * oracle. A key that holds is never echoed back.
 */
[TestClass]
[TestCategory("ApiKey")]
public class ApiKeyHandlerTests
{
	private const String Key = "0123456789abcdef";
	private const String Vector = "fedcba9876543210";

	// the store knows one key and one user; nothing else is asked of it
	private sealed class Store(String knownKey) : IUserLoginStore<AppUser<Int64>>, IUserClaimStore<AppUser<Int64>>
	{
		public Task<AppUser<Int64>?> FindByLoginAsync(String loginProvider, String providerKey, CancellationToken cancellationToken)
			=> Task.FromResult(providerKey == knownKey ? new AppUser<Int64>() { Id = 5 } : null);
		public Task<IList<Claim>> GetClaimsAsync(AppUser<Int64> user, CancellationToken cancellationToken)
			=> Task.FromResult<IList<Claim>>([]);

		public Task AddLoginAsync(AppUser<Int64> user, UserLoginInfo login, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task RemoveLoginAsync(AppUser<Int64> user, String loginProvider, String providerKey, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IList<UserLoginInfo>> GetLoginsAsync(AppUser<Int64> user, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task AddClaimsAsync(AppUser<Int64> user, IEnumerable<Claim> claims, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task ReplaceClaimAsync(AppUser<Int64> user, Claim claim, Claim newClaim, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task RemoveClaimsAsync(AppUser<Int64> user, IEnumerable<Claim> claims, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IList<AppUser<Int64>>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<String> GetUserIdAsync(AppUser<Int64> user, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<String?> GetUserNameAsync(AppUser<Int64> user, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task SetUserNameAsync(AppUser<Int64> user, String? userName, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<String?> GetNormalizedUserNameAsync(AppUser<Int64> user, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task SetNormalizedUserNameAsync(AppUser<Int64> user, String? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IdentityResult> CreateAsync(AppUser<Int64> user, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IdentityResult> UpdateAsync(AppUser<Int64> user, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IdentityResult> DeleteAsync(AppUser<Int64> user, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<AppUser<Int64>?> FindByIdAsync(String userId, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<AppUser<Int64>?> FindByNameAsync(String normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
		public void Dispose() { }
	}

	private static async Task<(AuthenticateResult Result, HttpContext Context)> AuthenticateAsync(String apiKey, String knownKey, Boolean skipCheckUser)
	{
		var store = new Store(knownKey);
		var config = Options.Create(new ApiKeyConfigurationOptions()
		{
			KeyType = KeyType.EncodedClaims,
			AesEncryptKey = Key,
			AesEncryptVector = Vector,
			SkipCheckUser = skipCheckUser
		});
		var handler = new ApiKeyAuthenticationHandler<Int64>(new OptionsMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default, store, store, config);
		var context = new DefaultHttpContext();
		context.Request.Headers[ApiKeyAuthenticationOptions.HeaderName] = apiKey;
		await handler.InitializeAsync(new AuthenticationScheme(ApiKeyAuthenticationOptions.DefaultScheme, null, typeof(ApiKeyAuthenticationHandler<Int64>)), context);
		return (await handler.AuthenticateAsync(), context);
	}

	private sealed class OptionsMonitor : IOptionsMonitor<ApiKeyAuthenticationOptions>
	{
		public ApiKeyAuthenticationOptions CurrentValue { get; } = new();
		public ApiKeyAuthenticationOptions Get(String? name) => CurrentValue;
		public IDisposable? OnChange(Action<ApiKeyAuthenticationOptions, String?> listener) => null;
	}

	// a new key on every call: the generator puts a Guid in it
	private static String ValidKey =>ApiKeyUserHelper<Int64>.GenerateApiKey(new AppUser<Int64>() { Id = 5 }, Key, Vector);

	// the last character of Base64Url carries the last byte of the last block: its padding
	private static String BadPadding(String key) => key[..^2] + (key[^2] == 'A' ? 'B' : 'A') + key[^1];

	[TestMethod]
	public async Task A_valid_key_is_not_echoed()
	{
		var valid = ValidKey;
		var (result, context) = await AuthenticateAsync(valid, valid, skipCheckUser: false);

		Assert.IsTrue(result.Succeeded, result.Failure?.Message);
		Assert.IsFalse(context.Response.Headers.ContainsKey("WWW-Authenticate"));
		Assert.IsFalse(context.Response.Headers.Values.Any(v => v.ToString().Contains(valid)));
	}

	[TestMethod]
	public async Task Every_broken_key_gets_the_same_refusal()
	{
		var valid = ValidKey;
		String[] broken =
		[
			BadPadding(valid),                                    // padding that does not hold
			AesEncrypt.EncryptString("not a user", Key, Vector),  // padding holds, the body is not JSON
			"!!not-base64!!",                                     // not Base64
			AesEncrypt.EncryptString("{\"Id\":7}", Key, Vector),  // a user the store does not know
		];
		foreach (var key in broken)
		{
			var (result, _) = await AuthenticateAsync(key, valid, skipCheckUser: false);
			Assert.IsNotNull(result.Failure, key);
			Assert.AreEqual("Invalid API key", result.Failure.Message, key);
		}
	}
}
