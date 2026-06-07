# Microsoft Store Submission Notes

## App Identity

Reserve the app name in Partner Center first. Use Partner Center's exact Package/Identity values when rebuilding the MSIX:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msix.ps1 `
  -IdentityName "PARTNER_CENTER_PACKAGE_IDENTITY_NAME" `
  -Publisher "PARTNER_CENTER_PACKAGE_PUBLISHER"
```

The current local package uses placeholder identity values and is suitable for packaging validation, not final Store upload.

## Suggested Listing

Short description:

```text
Vellwick Extractor is a simple Windows utility for extracting every zip archive inside a selected folder.
```

Long description:

```text
Vellwick Extractor gives Windows users a clean, focused way to extract zip files in bulk. Choose a folder, decide whether to keep or delete the original zip files, and run the extraction. The app scans subfolders, extracts each archive into its own collision-safe folder, protects against unsafe archive paths, and keeps a clear progress log while it works.

The interface is intentionally simple: paste or browse to a folder, review the keep-zip option, and press Execute. Vellwick Extractor also supports folder drag-and-drop, path autocomplete, dark mode, progress counters, and Vellwick-branded Windows shell extract actions after first launch.
```

Keywords:

```text
zip, extractor, unzip, archive, folders, batch extract, Vellwick
```

Category:

```text
Utilities & tools
```

Support URL:

```text
https://github.com/AlexanderTrysMine/vellwick-extractor/issues
```

Website:

```text
https://vellwick.com
```

Privacy policy:

```text
Vellwick Extractor does not collect, store, sell, or transmit personal data. The app checks GitHub releases for update availability and opens user-selected links only when requested.
```

## Notes

- Store installs are signed by Microsoft after approval and should avoid the Edge/SmartScreen `Publisher: Unknown` download warning.
- Direct GitHub `.exe` downloads remain a separate SmartScreen path unless the `.exe` is public-trust code signed or gains enough reputation.
- After Store approval, update the README download button to point to the Microsoft Store listing.
