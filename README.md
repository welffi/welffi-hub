# welffi hub

A Windows hub for welffi's programs. Each program is a tool inside it.

- **Account Manager**: log in to your Roblox accounts and launch them together.
- **welffi animate**: a Roblox R6 animator. The hub downloads it for you and tells you when there is an update.
- **Macros**: coming later.

## Get it

Download `whub.exe` from the latest release and run it. You need the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

To get welffi animate, click its tile on the Home page. The hub downloads it from [welffi-animate](https://github.com/welffi/welffi-animate) and keeps it in its own data folder. When a newer release is out, the tile says so and asks before updating.

## Building

Run `build.bat`, which makes `dist\whub.exe`. Close the hub first, since a running exe can't be replaced. The checks run with `dotnet run --project tests\Whub.Checks\Whub.Checks.csproj`.
