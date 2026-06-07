# Code Signing

Vellwick Extractor should be signed before public release to reduce Windows SmartScreen friction and show a verified publisher. Use `Vellwick` as the publisher/signing description.

## Important

Signing does not instantly guarantee SmartScreen will disappear. Microsoft SmartScreen still uses reputation, but signing lets reputation accrue to a consistent publisher identity across releases. Unsigned builds start from zero reputation every time.

## Option A: Azure Artifact Signing

Use this for GitHub Actions releases.

Required GitHub repository secrets:

- `AZURE_TENANT_ID`
- `AZURE_CLIENT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_TRUSTED_SIGNING_ENDPOINT`
- `AZURE_TRUSTED_SIGNING_ACCOUNT_NAME`
- `AZURE_TRUSTED_SIGNING_CERTIFICATE_PROFILE_NAME`

The Azure app/service principal must use GitHub Actions OIDC/federated credentials and must have the `Artifact Signing Certificate Profile Signer` role on the certificate profile.

Azure setup:

1. Create an Azure Artifact Signing account.
2. Complete Public Trust identity validation for `Vellwick`.
3. Create a Public Trust certificate profile.
4. Create a Microsoft Entra app registration for GitHub Actions OIDC.
5. Add a federated credential for this repository and the release branch.
6. Assign the app registration the `Artifact Signing Certificate Profile Signer` role on the certificate profile.
7. Add the required repository secrets above.

After those are configured:

1. Open GitHub Actions.
2. Run `Build Signed Release`.
3. Enter a new tag, such as `v1.0.5`.

## Option B: Local OV/PFX Certificate

Install Windows SDK Build Tools so `signtool.exe` is available.

Build:

```powershell
.\build.ps1
```

Sign from the certificate store:

```powershell
$env:VELLWICK_SIGN_CERT_THUMBPRINT = "YOUR_CERT_THUMBPRINT"
.\scripts\sign-release.ps1
```

Or sign from a PFX file:

```powershell
$env:VELLWICK_SIGN_PFX_PATH = "C:\path\to\certificate.pfx"
$env:VELLWICK_SIGN_PFX_PASSWORD = "pfx-password"
.\scripts\sign-release.ps1
```

The script signs `dist\Vellwick Extractor.exe` with SHA-256 and RFC3161 timestamping, then verifies the Authenticode signature.
