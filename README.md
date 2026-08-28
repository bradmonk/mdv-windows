# mdv-windows

`mdv-windows` is a Windows-native WPF Markdown viewer inspired by [tqbf/mdv](https://github.com/tqbf/mdv).

## Features

- **Markdown rendering** with [Markdig](https://github.com/xoofx/markdig) using CommonMark + advanced extensions (GFM tables, task lists, footnotes)
- **Persistent history sidebar** backed by SQLite, with delete support (right-click or Delete key)
- **File association helper** for `.md` files (per-user Windows registry association)
- **Find-in-document (Ctrl+F)** with live, character-by-character highlighting and next/previous navigation
- **Table of contents sidebar** for `h1/h2/h3`, with double-click navigation
- **Theme support** with built-in Dark, Light, and Reading themes
- **Bookmarks** using hotkeys:
  - Save: `Ctrl+0..Ctrl+5`
  - Restore: `Ctrl+Alt+0..Ctrl+Alt+5`
- **Live reload** using `FileSystemWatcher`
- **Drag-and-drop open** for `.md` files
- **Syntax highlighting** for code blocks via Prism.js language packs:
  - bash, c, go, javascript, python, ruby, rust, toml, yaml

## Architecture

Project layout follows MVVM with a split core/app structure:

- `/src/Mdv.Windows.Core`
  - `Models` — history, bookmarks, preferences, TOC, render result
  - `Services` — Markdown rendering, SQLite storage, file association
- `/src/Mdv.Windows.App`
  - `ViewModels` — `MainViewModel`
  - `Views` — WPF `MainWindow`
  - `Converters` — TOC indentation converter
- `/tests/Mdv.Windows.Core.Tests`
  - focused unit tests for rendering and storage

## Requirements

- .NET 8 SDK or newer
- Windows 10/11 for running the WPF application

## Build and run

From repository root:

```bash
dotnet restore Mdv.Windows.sln
dotnet build Mdv.Windows.sln
```

Run tests:

```bash
dotnet test tests/Mdv.Windows.Core.Tests/Mdv.Windows.Core.Tests.csproj
```

Run app on Windows:

```bash
dotnet run --project src/Mdv.Windows.App/Mdv.Windows.App.csproj -- "C:\\path\\to\\file.md"
```

## Notes

- User data is stored in:
  - `%LOCALAPPDATA%\\mdv-windows\\mdv.db`
- File association writes under:
  - `HKCU\\Software\\Classes\\.md`
  - `HKCU\\Software\\Classes\\mdv.windows`
