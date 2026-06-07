# Vellwick Extractor

Vellwick Extractor is a simple Windows desktop app for extracting zip files from a selected folder.

## Download

Download the latest Windows executable from the [GitHub releases page](https://github.com/AlexanderTrysMine/vellwick-extractor/releases/latest).

## Features

- Clean Windows UI with folder picker, progress bar, status counters, and detailed log.
- Editable folder path field with paste support, path autocomplete, and folder drag-and-drop.
- Vellwick-branded app icon/header mark and a clickable GitHub link.
- Recursively finds `.zip` files inside the selected folder and its subfolders.
- Extracts each zip into its own folder beside the original archive.
- Optional checkbox to keep or delete original zip files after successful extraction.
- Optional Windows right-click setup for `.zip` files: `Extract with Vellwick`.
- Collision-safe output names so existing files and folders are not overwritten.
- Basic zip-slip protection for unsafe archive paths.
- Single `.exe` build using the built-in .NET Framework compiler on Windows.

## Build

Run this from the project folder on Windows:

```powershell
.\build.ps1
```

The executable is written to:

```text
.\dist\Vellwick Extractor.exe
```

## Use

1. Open `Vellwick Extractor.exe`.
2. Paste, drag, or browse to the folder to scan.
3. Leave `Keep zip files after extraction` checked if you want to keep the original archives.
4. Click `Execute`.

Only zip files that extract successfully are deleted when the keep option is unchecked.

## Right-Click Extract

Open the app and click `Right-click setup` to add a per-user Windows Explorer action for zip files.

After setup, right-click a `.zip` file and choose `Extract with Vellwick`. The zip is extracted beside the original file and the original zip is kept.
