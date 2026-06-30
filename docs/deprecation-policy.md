# Deprecation Policy

ModelSync deprecations are introduced gradually.

## Policy

- Additive provider-specific APIs do not automatically deprecate older 1.x APIs.
- Existing Core default, check and index attributes remain supported in 1.x.
- Any future deprecation will include release notes, migration guidance and at least one minor release of overlap.
- Published package versions should not be overwritten or unlisted to hide compatibility issues; ship a fixed version and document the migration instead.
- Published package versions are not overwritten.
- Older package versions are not unlisted only to avoid supporting a migration path.
- Restore activity from NuGet CLI, MSBuild, CI, Artifactory and mirrors is considered when deciding compatibility windows, even though download counts are not unique user counts.
