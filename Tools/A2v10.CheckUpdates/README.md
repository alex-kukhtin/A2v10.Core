# A2v10.CheckUpdates

Build-time check for the [A2v10](https://a2v10.com) platform: tells you when a newer platform
generation is published.

All A2v10 packages are released together, so this package checks only **itself**: before each
build it compares its own version with the latest one on NuGet. If NuGet has a newer one, the build
prints a warning; nothing else changes.

## Install

Add it to the application module project (`MainApp`), not to the host:

```xml
<PackageReference Include="A2v10.CheckUpdates" Version="10.1.8668" />
```

It is a development dependency: nothing is referenced by the application and nothing flows to
projects that depend on it.

## What you see

```
warning A2V0001: A2v10 10.1.8670 is available; this application is on 10.1.8668.
Update the A2v10 skill first (the user's step), then update the platform to this version.
```

The order matters when the application is maintained with the A2v10 skill for an LLM assistant:
the skill carries the package versions it is written against, so an outdated skill would "update"
the platform to the versions it already has.

## Behavior

- Runs before the build, so the warning appears even when the build fails.
- One request to `api.nuget.org` with a 3-second timeout; the answer is cached for a day in
  `%LOCALAPPDATA%\A2v10` (`~/.local/share/A2v10` on Linux), shared by all applications on the
  machine.
- Never fails the build. Offline, behind a proxy, or with an unexpected answer, it stays silent;
  a failed request is cached too, so an offline machine pays the timeout once a day.
- Skipped in Visual Studio design-time builds.
- Prerelease versions are ignored.

## Turning it off

Skip the check entirely (no network request):

```xml
<PropertyGroup>
	<A2v10CheckUpdates>false</A2v10CheckUpdates>
</PropertyGroup>
```

Or keep the check and hide the warning: `<NoWarn>$(NoWarn);A2V0001</NoWarn>`.

## License

MIT.
