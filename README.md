# Glyph

Glyph is a Windows leader-key, layered key sequence engine with a discoverable overlay.

## Prerequisites
- Windows 10/11
- .NET SDK (8+)

## Build
`dotnet build Glyph.sln`

## Run
Because Glyph runs as a background app (global hook + overlay), `dotnet run` will keep running until you quit Glyph.

- Detached run (recommended): `run.bat`
- Attached run (foreground): `dotnet run --project src/Glyph.App/Glyph.App.csproj`

## Themes
Glyph supports built-in themes and user overrides.

- Select a built-in base theme by writing one of these into `%APPDATA%\Glyph\theme.base`:
	- `Default`
	- `FluentWin11`
	- `CatppuccinMocha`
- Optionally override any resources by creating `%APPDATA%\Glyph\theme.xaml` (a `ResourceDictionary`).

## Leader Key
- Default leader is `F12`.
- You can change it in the Settings window (tray icon → Settings). Glyph supports recording multi-stroke leaders.

## Config
Glyph persists settings to `%APPDATA%\Glyph\config.json`.
