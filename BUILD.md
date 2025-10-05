# Building More Strafts Players Mod

## Prerequisites

1. **.NET SDK** with netstandard2.1 support
   - Download from: https://dotnet.microsoft.com/download

2. **Straftat Game Files**
   - You must own the game on Steam

3. **Required Game DLLs**
   - These files are NOT included in the repository (proprietary)
   - You must extract them from your Straftat installation

## Setup

### 1. Create Libs Folder Structure

Create a `libs/` folder in the project root with the following DLLs from your Straftat installation:

```
libs/
├── Assembly-CSharp.dll
├── BepInEx.dll
├── 0Harmony.dll
├── FishNet.Runtime.dll
├── com.rlabrecque.steamworks.net.dll
├── UnityEngine.CoreModule.dll
├── UnityEngine.dll
└── (other Unity/game DLLs as needed)
```

**Where to find these files:**
- `<SteamLibrary>/steamapps/common/Straftat/BepInEx/core/` - BepInEx and Harmony
- `<SteamLibrary>/steamapps/common/Straftat/Straftat_Data/Managed/` - Game and Unity DLLs

### 2. Verify Project References

The `moreStrafts.csproj` file references DLLs from `.\libs\` relative to the project.

Ensure all references in the `.csproj` file point to existing DLLs, or adjust paths as needed.

## Building

### Command Line

```bash
cd /path/to/moreStrafts/src
dotnet build moreStrafts.csproj
```

The compiled `moreStrafts.dll` will be in `src/bin/Debug/netstandard2.1/` or `src/bin/Release/netstandard2.1/`

### Release Build

```bash
cd /path/to/moreStrafts/src
dotnet build moreStrafts.csproj -c Release
```

## Installation After Building

1. Copy `moreStrafts.dll` from `src/bin/Release/netstandard2.1/`
2. Paste into `<SteamLibrary>/steamapps/common/Straftat/BepInEx/plugins/`
3. Launch the game

## Troubleshooting

### Missing Reference Errors
- Ensure all DLLs are in the `libs/` folder
- Check that DLL versions match your game version
- Verify paths in `moreStrafts.csproj`

### Build Fails with "Could not find SDK"
- Install .NET SDK with netstandard2.1 support
- Restart terminal/IDE after installation
