# Code signing policy

ShieldGate ships a single Windows executable, `ShieldGate.exe`. This document
describes which builds may be signed, how they are produced, and how a user can
verify a signature. It is also the signing policy referenced from the repository
and from the SignPath Foundation application.

## Status

Code signing is **prepared but not active yet**. The repository is ready for the
[SignPath Foundation](https://signpath.org/) free open-source signing programme,
but the application still has to be submitted by the maintainer. Until that
application is accepted and the repository variables and secret below are
configured, the `sign` job in `.github/workflows/build.yml` is skipped and release
files remain unsigned. The build itself, the self-test and the release process do
not depend on signing.

## What is signed

Only the following artifact is eligible for signing:

| Item | Value |
|---|---|
| File name | `ShieldGate.exe` |
| Project | `shieldgate` |
| Repository | <https://github.com/GnasValluk/shieldgate> |
| Source | the commit pointed to by a `v*` tag on `main` |
| Build system | GitHub Actions, `.github/workflows/build.yml` |
| Runner | `windows-latest` (GitHub-hosted) |
| Signing service | SignPath Foundation, Authenticode |
| Distribution | GitHub Releases only |

Nothing else is signed: no pull-request artifact, no local developer build, no
test executable, no third-party binary, and no file from `viruts/` or from a
quarantine folder.

## How a signed build is produced

1. A maintainer pushes a tag such as `v3.0.2` that points to a commit on `main`.
2. GitHub Actions checks out that commit and runs `build.ps1`, which compiles
   `src/core` and `src/ui` with the .NET Framework 4.0 `csc.exe`.
3. `test.ps1` runs the self-test; a failing test stops the workflow.
4. The unsigned `dist/ShieldGate.exe` is uploaded as a GitHub Actions artifact.
5. The `sign` job sends that artifact ID to SignPath. SignPath checks that the
   artifact came from this repository's GitHub Actions run before signing it.
6. The signed file is downloaded, uploaded as the `ShieldGate-signed` artifact,
   and attached to the matching GitHub Release if that release already exists.

The signing job only runs for tag pushes and only when the repository variable
`SIGNPATH_PROJECT_SLUG` is set. Pull requests and ordinary branch pushes never
reach the signing service.

## Repository configuration

The workflow reads these values. They are repository variables, not files, so no
credential is committed:

| Name | Kind | Purpose |
|---|---|---|
| `SIGNPATH_ORGANIZATION_ID` | variable | SignPath organization ID |
| `SIGNPATH_PROJECT_SLUG` | variable | SignPath project slug, for example `shieldgate` |
| `SIGNPATH_SIGNING_POLICY_SLUG` | variable | signing policy, for example `release-signing` |
| `SIGNPATH_API_TOKEN` | secret | API token for a SignPath submitter |

The SignPath project uses an artifact configuration whose root is a ZIP archive
(the format produced by `actions/upload-artifact`) containing one PE file:

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
  <zip-file>
    <pe-file path="ShieldGate.exe">
      <authenticode-sign/>
    </pe-file>
  </zip-file>
</artifact-configuration>
```

The exact slug and policy names are chosen in the SignPath dashboard; the values
above are the defaults used by the workflow. If a different slug is chosen, update
the repository variable, not the workflow.

## Verification

On Windows:

```powershell
Get-AuthenticodeSignature .\ShieldGate.exe | Format-List Status, SignerCertificate
Get-FileHash .\ShieldGate.exe -Algorithm SHA256
```

A signed release shows `Status : Valid` and a signer certificate issued to the
SignPath Foundation. The SHA-256 must match the value in the release notes. A file
with `Status : NotSigned` is a build made before the signing programme was
enabled, or a file that was not downloaded from GitHub Releases.

## Who may sign

Only the repository maintainer (`GnasValluk`) pushes release tags. The SignPath
API token is stored as an encrypted GitHub Actions secret and is available only
to workflows on the default branch and on tags. The token is not available to
pull requests from forks. SignPath keeps the private key in its HSM; no private
key is stored in this repository or in GitHub Actions.

## Defensive purpose

ShieldGate is a defensive end-user tool. It scans removable drives for shortcut
viruses, quarantines suspicious files with user confirmation, removes the traces
those viruses leave on the PC, repairs drive metadata, and inventories
peripherals. It is not a penetration-testing tool, an exploit, a privilege
escalation tool, or a way to bypass Windows security. It does not disable
Defender, does not add Defender exclusions, does not write to the system drive,
and has no network access unless the user opts in to the VirusTotal hash lookup.
The source is public under the MIT licence.

## Application answers

These values can be pasted into the form at <https://signpath.org/apply.html>:

| Field | Value |
|---|---|
| Project name | ShieldGate |
| Repository | <https://github.com/GnasValluk/shieldgate> |
| Licence | MIT |
| Releases | <https://github.com/GnasValluk/shieldgate/releases> |
| Build system | GitHub Actions, `.github/workflows/build.yml`, `windows-latest` |
| Signed file | `ShieldGate.exe` from a `v*` tag only |
| Project summary | ShieldGate is a portable Windows guard for USB drives and peripherals. It scans removable media, quarantines suspicious files with user confirmation, removes shortcut-virus traces, repairs drive metadata and inventories devices. It is offline by default and has no installer, driver or telemetry. |
| Why code signing | The executable is distributed directly from GitHub Releases. A valid Authenticode signature lets users verify the publisher and reduces SmartScreen and antivirus false positives without asking anyone to disable protection. |

## Applying to SignPath Foundation

1. Open <https://signpath.org/apply.html> and submit the repository URL, the MIT
   licence, the Releases page, and the defensive-purpose paragraph above.
2. Wait for the acceptance email. Approval usually takes days to two weeks.
3. In the SignPath dashboard, create an organization and a project linked to the
   GitHub repository through the [SignPath GitHub App](https://github.com/apps/signpath).
4. Create the artifact configuration shown above and a signing policy for
   release tags.
5. Add the three variables and the API token secret in GitHub under
   **Settings → Secrets and variables → Actions**.
6. Push a test tag, confirm that the `sign` job runs, then verify the downloaded
   file with `Get-AuthenticodeSignature`.

Until step 6 succeeds, the release notes must say that the executable is
unsigned and must include its SHA-256.
