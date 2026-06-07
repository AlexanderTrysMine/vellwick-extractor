# Vellwick Extractor

Vellwick Extractor is a simple Windows desktop app for extracting zip files from a selected folder.

<p>
  <a href="https://github.com/AlexanderTrysMine/vellwick-extractor/releases/latest/download/Vellwick.Extractor.exe">
    <img alt="Download Vellwick Extractor for Windows" src="https://img.shields.io/badge/Download%20Vellwick%20Extractor-Windows%20.exe-2563eb?style=for-the-badge&logo=windows&logoColor=white" />
  </a>
</p>

## Download

Click the big download button above, or download the latest Windows executable from the [GitHub releases page](https://github.com/AlexanderTrysMine/vellwick-extractor/releases/latest).

Microsoft Store packaging is being prepared for a cleaner non-technical install path. Store installs use Microsoft signing; direct GitHub `.exe` downloads are a separate SmartScreen reputation path.

## Features

- Clean dark-mode Windows UI with native Windows folder picker, progress bar, status counters, and detailed log.
- Editable folder path field with paste support, path autocomplete, and folder drag-and-drop.
- Vellwick-branded app icon/header mark using the logo asset from `vellwick.com`.
- Clicking the `Vellwick Extractor` header opens `vellwick.com`.
- Windows executable icon for Explorer, search, and file picker views.
- Clickable GitHub link with GitHub mark.
- Checks the latest GitHub release on startup and can install newer versions automatically.
- Recursively finds `.zip` files inside the selected folder and its subfolders.
- Extracts each zip into its own folder beside the original archive.
- Surfaces redundant single-folder wrappers after extraction when the extracted folder contains only one child folder and no files.
- Optional checkbox to keep or delete original zip files after successful extraction.
- Automatic Windows right-click actions:
  - `.zip` files: `Extract with Vellwick`
  - folders and folder backgrounds: `Extract all with Vellwick`
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

## Build MSIX

To create a Microsoft Store MSIX package layout, install MSIX Toolkit or Windows SDK and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msix.ps1
```

The MSIX is written to:

```text
.\dist\VellwickExtractor.msix
```

Before final Store submission, rebuild with the exact Package/Identity values from Partner Center. See [MSIX.md](MSIX.md).

## Use

1. Open `Vellwick Extractor.exe`.
2. Paste, drag, or browse to the folder to scan.
3. Leave `Keep zip files after extraction` checked if you want to keep the original archives.
4. Click `Execute`.

Only zip files that extract successfully are deleted when the keep option is unchecked.

## Right-Click Extract

Open the app once to refresh the per-user Windows Explorer actions automatically.

Right-click a `.zip` file and choose `Extract with Vellwick`, or right-click a folder and choose `Extract all with Vellwick`. Right-click extraction keeps the original zip files.
