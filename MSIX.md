# MSIX / Microsoft Store Packaging

Microsoft Store distribution and GitHub direct-download distribution are separate trust paths.

If Vellwick Extractor is installed through the Microsoft Store as an MSIX package, Microsoft signs the Store package during submission. That Store install path avoids the Edge/SmartScreen "Publisher: Unknown" download warning.

That does not automatically make the raw GitHub `.exe` trusted. A direct GitHub `.exe` download still needs public-trust code signing or enough SmartScreen reputation. After Store approval, the GitHub README download button should point to the Microsoft Store listing for non-technical users.

## Build MSIX

Install the MSIX Toolkit or Windows SDK so `MakeAppx.exe` is available, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msix.ps1
```

The package is written to:

```text
.\dist\VellwickExtractor.msix
```

## Partner Center Values

Before final Store submission, reserve the app in Partner Center and use the exact Package/Identity values that Partner Center provides:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-msix.ps1 `
  -IdentityName "PARTNER_CENTER_PACKAGE_IDENTITY_NAME" `
  -Publisher "PARTNER_CENTER_PACKAGE_PUBLISHER"
```

For sideloading outside the Store, the MSIX still needs a trusted public signature. A self-signed MSIX is only useful for local testing after trusting the certificate on the test machine.
