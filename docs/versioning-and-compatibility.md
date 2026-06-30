# Versioning and Compatibility

ModelSync follows semantic versioning.

## 1.x Compatibility

- Existing public APIs remain source-compatible throughout the 1.x line unless a security issue requires otherwise.
- Legacy Core attributes remain supported in 1.x.
- New provider-specific APIs are additive.
- Public result contracts may grow additively with provider-neutral fields.
- Core must not expose provider exception types in public contracts.

## NuGet Consumption

ModelSync packages are consumed by NuGet CLI, MSBuild, CI systems, Artifactory and mirror clients. Download counts are not unique user counts. Even so, older package versions continuing to restore is treated as a compatibility signal: migration documentation, source compatibility and clear deprecation windows are required.

Published package versions should not be overwritten. Older package versions should not be unlisted as a replacement for fixing compatibility and documenting migration behavior.

## Release Gates

Before a release, the repository should pass:

- Release build.
- Non-integration tests.
- Repository checks.
- Package smoke validation.
- Provider integration tests when the environment is available.

## NuGet Consumers

ModelSync packages may be consumed by NuGet CLI, MSBuild, CI agents, Artifactory and mirror clients. Download counts are not unique users. Continued restore activity still means public APIs and migration documentation must remain disciplined across the 1.x line.
- 1.2.0 consumer compatibility checks for legacy and canonical consumers.
