# Gensokyo Launcher

A minimal Avalonia launcher for discovering and running official Windows and PC-98 Touhou games on Windows and Linux. It does not include any games or patches.

## Development

Requires the .NET 10 SDK.

```sh
dotnet restore
dotnet build
dotnet test
dotnet run --project src/GensokyoLauncher
```

On Linux, Windows executables are launched through `wine` by default. The command can be changed in Settings.

THCRAP is downloaded from the official `thpatch/thcrap` GitHub releases into the current user's local application-data directory. Network phases have bounded timeouts so setup can be retried instead of remaining stuck indefinitely.

The stable Neko Project II/W binary is downloaded from the official Neko Project 21/W distribution and installed alongside THCRAP in local application data. Recognized Touhou 1–5 disk images (`.hdi`, `.d88`, `.fdi`, and related PC-98 formats) are launched through it. Game and BIOS images are never downloaded or bundled.
