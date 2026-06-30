# Consumer Compatibility Evidence Template

Candidate version: supplied by release gate
Candidate source: supplied explicitly by release command
Final validation version: `1.2.1`

## Baseline

- Source: `NuGet.org`
- NuGet.org package version: `1.2.0`
- Restore: `PASS`
- Build: `PASS`
- Warnings: `0`
- Errors: `0`
- TreatWarningsAsErrors: `PASS`

## Candidate Legacy Consumer

- Source: supplied explicitly by release command
- ModelSync version: supplied by release gate
- Restore: `PASS`
- Build: `PASS`
- Warnings: `0`
- Errors: `0`
- NewWarningDelta: `0`
- TreatWarningsAsErrors: `PASS`

## Canonical Provider Consumer

- Source: supplied explicitly by release command
- ModelSync version: supplied by release gate
- Restore: `PASS`
- Build: `PASS`
- Warnings: `0`
- Errors: `0`
- TreatWarningsAsErrors: `PASS`

## Source Integrity

- ProjectReferenceDetected: `false`
- UnexpectedPackageSourceDetected: `false`
- MixedModelSyncVersionsDetected: `false`
