# 06 — Identity, Web, Mcp, Cli, Scheduling, BlobStorages, Messaging, тесты, гигиена решения

Учтено как решения: MCP DCR stateless, семейство refresh целиком, нет RotateToken multi-tenant, VisibleTools отложены; FIFO Scheduling; AppUserAdmin без tenant. A2v10.Mcp (OAuthController, OAuthProtector, OAuthConsent, McpToolHandlers) — чисто.

ВЫСОКАЯ
1. ApiKey EncodedClaims: AES-CBC без MAC, ключ = первые 16 байт UTF-8 строки, статический IV (Identity/A2v10.Identity.Core/Helpers/AesEncrypt.cs:17-18, 31-45); ApiKeyUserHelper.cs:44-48 сразу JsonSerializer.Deserialize; ApiKeyAuthenticationHandler.cs:64-71 ловит только CryptographicException (401) и FormatException; JsonException → 500 → padding oracle (расшифровка, CBC-R подделка Id). SkipCheckUser=true (A2v10.ApiHost/Startup.cs:126) — нет проверки в БД, отзыв невозможен.
2. Open redirect: AccountController.cs:225-228 (Login), :927-930 (TwoFactor) — returnUrl проверяется только на префикс /account; клиент window.location.replace (LoginScript.cshtml:61, TwoFactorScript.cshtml:32). Нужен Url.IsLocalUrl.
3. Регистрация открыта при Application:Registration=false: флаг только в Views/Account/Login.cshtml:51; Register GET/POST (AccountController.cs:246-331), ConfirmCode/confirmemail всегда доступны.
4. Внешний логин привязывается по email без email_verified (AccountController.cs:747-758), bypassTwoFactor: true (:776); OpenIdErrorHandlers.cs:33-51 EnsureExternalLogin на каждый тикет — создаёт пользователя EmailConfirmed=1, Tenant=1 (a2v10_security.sql:207-220). nOAuth-риск.
5. ApiKeyAuthenticationHandler.cs:61 — WWW-Authenticate = сам ключ в ответе.
6. Перебор email-кода: CheckForgotPasswordCode (AccountController.cs:385-401), ResetPassword (:403-429), ConfirmCode (:634) — VerifyTwoFactorTokenAsync без счётчика/lockout.
7. A2v10.ApiHost (в sln): GenerateApiKey [AllowAnonymous] выдаёт ключ для пользователя 99; CurrentUser.Id => 99; AgentController/WaybillController без [Authorize]; Startup.cs:133-138 DeveloperExceptionPage и Swagger безусловно.

СРЕДНЯЯ
8. AccountController.cs:817 срок InitPassword инвертирован (разность всегда отрицательна).
9. AppUserStore.cs:676-686 счётчик неудачных входов — read-modify-write абсолютным числом, гонка обходит lockout.
10. a2v10_security.sql:168-172 AllowIP возвращается, но в C# не используется, @Host не передаётся (AppUserStore.cs:473-479); ApiKey хранится открытым текстом.
11. DataProtection-ключи в SQL без ProtectKeysWith* (Identity.Core/ServicesExtensions.cs:176-190, DataProtection/SqlServerDataProtectionRepository.cs:36-51).
12. JWT: ValidateIssuer/Audience=false (Identity.Jwt/JwtBearerSettings.cs:81-82), RequireHttpsMetadata=false (:54); JwtBearerService.cs:84 ExtractPrincipalFromToken без lifetime (не используется). Автор знает (A2v10.Mcp/CLAUDE.md), но пакет опубликован.
13. AppUserStore возвращает «пустого» пользователя вместо null (:104-108, 118-120, 263-264) — нарушение контракта IUserStore; ~15 `?? throw` мертвы; SetPasswordHandler.cs:33-56 Success=true с Id=0; TwoFactorAuthenticatorHandler.cs:27/72/110 SQL с Id=0; FindByIdAsync (:104) бросает на нечисловой строке.
14. AppUserStore.cs:436-439 ReplaceClaimAsync игнорирует newClaim; UpdateClaim (:391) имя процедуры из claim.Type.
15. JsonResponse.cs:18-21 Error(Exception) отдаёт ex.Message анонимному клиенту (Login :241, Register :329 и др.); confirmemail NotFound(ex.Message) (:674).
16. Перечисление пользователей: EmailNotConfirmed до проверки пароля (AccountController.cs:205-206), result.ToString() (:236) раскрывает LockedOut; ForgotPassword NotAllowed (:362-363).
17. Messaging/A2v10.MailClient/A2v10 - Backup.MailClient.csproj рядом с основным (net6/7, 8109) — MSB1011, общий obj.
18. Сироты вне sln: A2v10.Web.Services (net5, битая ссылка), Web/A2v10.Core.Web.Mvc (net5, копии VueDataScripter/ShellController), Implementation/A2v10.Ip2Sms (net6, пустой), Obsolete/A2v10.AppRuntimeBuilder (битая ссылка; закомментированный вызов Web.Site/Startup.cs:63), Stimulsoft (net6). publishnuget.cmd копирует несуществующий Platform\A2v10.AppRuntimeBuilder; Stimulsoft: rem на удаление, копирование оставлено; дубль del A2v10.Services.
19. FileSystemBlobStorage.cs:22,33,50 Path.Combine без проверки корня; :36 синхронный CopyTo; AzureBlobStorage.cs:50 падает при существующем blob, FS перезаписывает — LSP; нет null-проверок.
20. MailClient.cs:88 SmtpClient не освобождается; :87,:105 адреса в Information (PII); AuthenticateAsync безусловно.
21. LicenseBuilder/CommandProcessor.cs:86 genkeys перезаписывает приватный ключ; :47-57 исключения глотаются, код 0.

НИЗКАЯ
22. Без antiforgery: DarkMode (AccountController.cs:122-135); ChangePassword (:549-551) [Authorize]+[AllowAnonymous]; Logout по GET (:511-517); DemoController GET /demo в опубликованном пакете (login-CSRF).
23. TwoFactor POST :919-921 лишний VerifyTwoFactorTokenAsync для произвольного login; :232 login в URL без кодирования.
24. Один DataProtector purpose «Login» (:49) на ConfirmCode (:309), confirmemail (:267), InitPassword (:791).
25. Scheduling: исключение аргументом шаблона (GenericJob.cs:44, ProcessCommandsJobHandler.cs:38,55, ExecuteSqlJobHandler.cs:26); ExecuteSqlJobHandler глотает ошибку; мёртвые GetSection()==null (Scheduling/ServicesExtensions.cs:20, Identity.Core/ServicesExtensions.cs:141).
26. CLI: TablesCommand.cs:45-46 AND/OR без скобок; ColumnsCommand.cs:46-47 условие дважды; "null:" Program.cs:114 и литерал App/AppConfigCommand.cs:60; Contains("A2v10.") (:62).
27. DRY: "a2security" ×4 (AppUserStore.cs:37, AppUserStoreOptions.cs:20, SqlServerDataProtectionRepository.cs:20, OpenIdErrorHandlers.cs:41); "ApiKey" (ApiKeyAuthenticationHandler.cs:31, AppUserStore.cs:473); AesEncrypt:Key/Vector дважды (ApiKeyConfigurationOptions.cs:23-31, ApiKeyUserHelper.cs:30-37); 4 сборки параметров токена (AppUserStore.cs:555-619); SetPasswordHandler vs AppUserAdmin.SetPasswordAsync; TwoFactorDisableHandler vs AppUserAdmin.ResetTwoFactorAsync; DI-список CLI (Program.cs:88-147) vs Tests/TestsMetadata/TestHost.cs:45-90.
28. AuthenticatorIssuer = "NovaEra" (Identity.Core/ServicesExtensions.cs:41); URI email литералом (OpenIdErrorHandlers.cs:44); пароль ≥6 без сложности (:27-31).
29. Закомментированное: AccountController.cs:172-187, 311-317; AppUserStore.cs:32,328,339-340,365-376; Identity.Core/ServicesExtensions.cs:58,66,69-72; Web.Site/Startup.cs:60-68,134-141; ApiHost.Tests/ApiTestAppFactory.cs:53-61; ApiHost/Controllers/ApiController.cs:28. ApiKeyAuthenticationHandler.cs:88-96 переопределения = base. 8 NotImplemented в AppUserStore + ветка RolesMode.Database.
30. Тесты: TestServices/JsStaticFunctions.cs:15-25 пустой; ApiHost.Tests/UnitTest1.cs:11-25 без assert, нужна живая БД, TextFile1.txt; нет тестов Identity/ApiKey/JWT/MCP OAuth/Scheduling/Blob/Mail. TestsMetadata 235 тестов/~550 assert — качественные. Фреймворки: MSTest 4.5.1, xunit.v3 3.2.2, xunit.v3 4.0.0.
31. Корень: bundleconfig.json 0 байт; description.txt CP1251; todo.txt 113 строк устаревшего; screen-loading.md вне карты; Web.Site/Program.cs:1 битая кодировка; Identity.Core и A2v10.Mcp на Sdk.Web.
Секреты: нет, только плейсхолдеры (Web.Site/appsettings.json MailSettings:Password, GoogleMapsApiKey; ApiHost/appsettings.json JwtBearer:SecurityKey).

CSPROJ: нет Directory.Build.props/Directory.Packages.props/global.json. Активные согласованы (Microsoft.* 10.0.12, A2v10.Data.* 10.1.7625, SourceLink 10.0.401). Сироты: Web.Mvc net5 8012, Web.Services net5, MailClient Backup net6;7 8109, Ip2Sms net6, Obsolete/AppRuntimeBuilder 8636, Stimulsoft net6 8085. Scheduling.Infrastructure 8630, App.Infrastructure 8634, Module.Infrastructure 8630 netstandard2.0, Xaml.Report 8663. A2v10.Platform — Nullable не задан. ImplicitUsings вперемешку; LangVersion не задан в MailClient, LicenseBuilder, TestMailSender, ApiHost.Tests. Хосты: net8 (Site, CLI) и net10 (ApiHost, TestsMetadata).

Общая: новый код (Mcp, CLI, Scheduling) дисциплинирован; риск — старый Identity (опубликованные пакеты), без тестов.
