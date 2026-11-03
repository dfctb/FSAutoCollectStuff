# Fallout Shelter Auto Collect Stuff

BepInEx 5 plugin for Fallout Shelter: automatic collection of resident levels,
completed training sessions and completed births.

## Installation
1. Install BepInEx 5 (x64, Mono).
2. Copy `Krimson_AutoCollect.dll` to `BepInEx/plugins/`.
3. Launch the game and configure the settings in `BepInEx/config/krimson.fos.autocollect.cfg`.

## Building from source
Specify the paths to the game in `Krimson_AutoCollect.csproj` (`HintPath` for
`Assembly-CSharp.dll`, `UnityEngine.dll`, `BepInEx.dll`, `0Harmony.dll`)
and build using `dotnet build -c Release`.

## Licence
MIT (see LICENSE).