---
name: unsga3-release
description: >
  Cut an Unsga3 release: version bump, CHANGELOG, tests, git tag v*, push, GitHub Release,
  and GitHub Packages publish. Use when the user runs /unsga3-release, or asks to ship,
  tag, publish NuGet/GitHub Packages, bump version, or cut a release for Unsga3.
---

# Unsga3 release

Repo: `AppSprout-dev/Unsga3`. Work from the Unsga3 root. Confirm clean (or intentional) git status first.

## Preconditions

- [ ] `dotnet test Unsga3.slnx -c Release` green
- [ ] User has confirmed the **semver** bump (patch / minor / major) and release notes tone
- [ ] No secrets in the tree; `tools/oracle/out/` not staged

Optional before a quality release: `/unsga3-oracle` full multi-seed if survival/normalization/metrics changed.

## Version sources of truth

Bump **all** of these to the same version `X.Y.Z`:

1. `Directory.Build.props` → `<Version>X.Y.Z</Version>`
2. `CHANGELOG.md` → move **Unreleased** into `## [X.Y.Z] — YYYY-MM-DD`, leave empty Unreleased
3. `CITATION.cff` → `version:` + `date-released`
4. `README.md` status line if it hardcodes a version

Tag format: **`vX.Y.Z`** (leading `v`). Publish workflow triggers on `v*`.

## Steps

1. **Changelog** — Keep a Changelog style; user-visible Fixed/Added/Changed only.
2. **Version bump** — files above.
3. **Test**
   ```powershell
   dotnet test Unsga3.slnx -c Release
   ```
4. **Commit** (only release files + intentional code)
   ```powershell
   git add Directory.Build.props CHANGELOG.md CITATION.cff README.md
   # plus any shipped code/docs
   git commit -m "Release vX.Y.Z"
   ```
5. **Tag + push**
   ```powershell
   git tag vX.Y.Z
   git push origin main
   git push origin vX.Y.Z
   ```
6. **GitHub Release**
   ```powershell
   gh release create vX.Y.Z --title "vX.Y.Z — <short title>" --notes-file - 
   ```
   Or paste notes from CHANGELOG. Link `docs/WILCOXON-RESULTS.md` / oracle if relevant.
7. **Verify publish**
   - Actions: `publish-github-packages` on the tag must succeed
   - Package: `https://github.com/orgs/AppSprout-dev/packages` (Unsga3)

## Do not

- Force-push `main` or move a published tag without explicit user request
- Publish to nuget.org unless that path is already set up (still open issue unless closed)
- Skip tests “because docs-only” if `Version` or library code changed
- Bump version without a matching `v*` tag (package version and tag must match)

## Rollback note

If the tag published a bad package: fix forward with `vX.Y.(Z+1)`; do not delete GitHub Packages versions casually.
