# Build AetherXIV on Windows

This build produces the complete Windows 11 x64 release: Core, Launcher, server
services, database tooling, client helpers, and Umbra.

## Requirements

- Windows 11 x64
- PowerShell 5.1 or [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows)
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), exact SDK
  `10.0.203`
- [Python 3](https://www.python.org/downloads/)
- One native C++ toolchain:
  - [Visual Studio Build Tools](https://visualstudio.microsoft.com/downloads/)
    with MSBuild, the MSVC v143 C++ toolset, and a Windows SDK; or
  - [MSYS2](https://www.msys2.org/) or another MinGW-w64 distribution providing
    `i686-w64-mingw32-g++` on `PATH`
- Internet access for the initial NuGet restore

The build prefers Visual Studio MSBuild and falls back to MinGW-w64.

Confirm the tools in PowerShell:

```powershell
dotnet --list-sdks
python --version
```

When using MinGW, also run:

```powershell
i686-w64-mingw32-g++ --version
```

## Build

From the repository root:

```powershell
.\tools\Windows\build-aetherxiv.ps1 -Configuration Release
```

For a development package:

```powershell
.\tools\Windows\build-aetherxiv.ps1 -Configuration Debug
```

`ServerRid` and `LauncherRid` default to `win-x64` and can be passed explicitly
for a reviewed alternate build.

## Output

The complete release is written to `bin\build\Release\Windows`:

```text
Windows\
├── core\app\AetherXIV.Core.App.exe
├── launcher\app\AetherXIV.Launcher.App.exe
├── servers\
└── Database\
```

The Launcher contains self-contained x64 and x86 helpers. Umbra's bootstrap is
always built for the legacy x86 game process.

## Output reset warning

The script recreates the selected Windows release and removes unexpected
top-level content beneath `bin`. Keep all personal files and diagnostics outside
`bin`.

## Verification

The Windows script checks its required release files before reporting success.
Run all tests from a Bash-capable development environment with:

```bash
./tools/Development/verify-aetherxiv.sh
```

Alternatively, build and test both solution files directly with the pinned SDK.

## Common failures

- Install the **Desktop development with C++** workload components if MSBuild is
  found but the Win32 projects cannot compile.
- If using MSYS2, ensure the compiler's `bin` directory is visible to the
  PowerShell process running the build.
- Python may be discovered as `python3`, `python`, or `py -3`; set `PYTHON_BIN`
  when discovery selects the wrong installation.
