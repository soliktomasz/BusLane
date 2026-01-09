# Release Process Documentation

## Overview

The `release_script.sh` automates the entire release process for BusLane, ensuring version consistency across all project files.

## Features

- ✅ **Version Detection**: Automatically detects current versions from:
  - Git tags
  - README.md badge
  - Info.plist (macOS bundle)
  - GitHub Pages (docs/index.html)

- ✅ **Multi-file Updates**: Updates version in all locations:
  - README.md version badge
  - BusLane/Info.plist (CFBundleVersion and CFBundleShortVersionString)
  - docs/index.html (hero section and roadmap)

- ✅ **Interactive**: Prompts for confirmation before each major step

- ✅ **Safe**:
  - Validates version format
  - Checks for uncommitted changes
  - Verifies tag doesn't already exist
  - Shows diff before committing

## Usage

### Option 1: Interactive Mode (Recommended)

Run without arguments to enter interactive mode:

```bash
./release_script.sh
```

The script will:
1. Display current versions from all files
2. Prompt you to enter the new version
3. Show what will be changed
4. Ask for confirmation at each step

### Option 2: Direct Version

Provide version as an argument:

```bash
./release_script.sh 0.7.2
```

## Version Format

The script accepts semantic versioning:

- **Stable releases**: `MAJOR.MINOR.PATCH`
  - Examples: `0.7.2`, `0.8.0`, `1.0.0`

- **Pre-releases**: `MAJOR.MINOR.PATCH-PRERELEASE`
  - Examples: `0.8.0-beta.1`, `1.0.0-rc.1`, `0.7.2-alpha`

## Step-by-Step Process

When you run the script, it will:

### 1. Show Current Versions
```
╔════════════════════════════════════════╗
║      BusLane Release Manager          ║
╚════════════════════════════════════════╝

==> Current Version Information

  Git Tag:       0.7.1
  README.md:     0.7.1
  Info.plist:    0.7.1
  GitHub Pages:  0.7.1
```

### 2. Get New Version
- Prompts for new version (if not provided as argument)
- Validates version format
- Checks if tag already exists

### 3. Safety Checks
- Ensures you're on main branch (or confirms if you want to proceed)
- Verifies no uncommitted changes exist

### 4. Show Update Preview
```
==> The following files will be updated:

  1. README.md
     Version badge: 0.7.1 → 0.7.2

  2. BusLane/Info.plist
     CFBundleVersion: 0.7.1 → 0.7.2
     CFBundleShortVersionString: 0.7.1 → 0.7.2

  3. docs/index.html (GitHub Pages)
     Hero version: 0.7.1 → 0.7.2
     Current badge: v0.7.1 → v0.7.2

Proceed with version update? (y/n)
```

### 5. Update Files
- Updates README.md
- Updates Info.plist
- Updates GitHub Pages

### 6. Review Changes
- Shows git diff of all changes
- Prompts for commit confirmation

### 7. Commit & Tag
- Creates commit with descriptive message
- Creates annotated git tag
- Prompts to push tag
- Prompts to push commit

### 8. Success
```
╔════════════════════════════════════════╗
║     Release v0.7.2 Created! 🎉        ║
╚════════════════════════════════════════╝

✓ Version updated in all locations:
  ✓ README.md
  ✓ BusLane/Info.plist
  ✓ docs/index.html

[INFO] GitHub Actions will now build and publish the release.
[INFO] Monitor progress at: https://github.com/soliktomasz/BusLane/actions
```

## Files Updated

### README.md
```markdown
![Version](https://img.shields.io/badge/Version-0.7.2-blue)
```

### BusLane/Info.plist
```xml
<key>CFBundleVersion</key>
<string>0.7.2</string>
<key>CFBundleShortVersionString</key>
<string>0.7.2</string>
```

### docs/index.html
```html
<!-- Hero section -->
<span>Version 0.7.2 — Enhanced Message Management</span>

<!-- Roadmap section -->
<span class="version-badge current">v0.7.2</span>
```

## Manual Steps (If Needed)

If you need to update versions manually or the script fails:

1. **README.md**: Update the version badge on line 3
2. **Info.plist**: Update both `CFBundleVersion` and `CFBundleShortVersionString`
3. **docs/index.html**:
   - Update hero badge (search for "Version X.X.X")
   - Update current version badge (search for "version-badge current")
4. Create git commit: `git commit -m "chore: bump version to X.X.X"`
5. Create tag: `git tag -a vX.X.X -m "Release X.X.X"`
6. Push: `git push origin vX.X.X && git push origin main`

## Troubleshooting

### "You have uncommitted changes"
```bash
# Stash your changes
git stash

# Run release script
./release_script.sh 0.7.2

# Apply stashed changes
git stash pop
```

### "Tag already exists"
```bash
# Delete local tag
git tag -d v0.7.2

# Delete remote tag (if pushed)
git push origin :refs/tags/v0.7.2

# Run release script again
./release_script.sh 0.7.2
```

### "Not on main branch"
The script will warn you but allow you to proceed if you confirm. This is useful for testing releases on feature branches.

### Version Mismatch
If the script shows different versions in different files, it's safe to proceed. The script will update all files to match the new version you provide.

## After Release

Once you push the tag, GitHub Actions will automatically:

1. Build the application for all platforms
2. Run tests
3. Create a GitHub release
4. Upload build artifacts
5. Publish release notes

Monitor the workflow at: https://github.com/soliktomasz/BusLane/actions

## Notes

- The script uses MinVer for automatic versioning from git tags
- The actual application version at runtime is determined by MinVer
- Manual version updates in files are for documentation and display purposes
- Always tag releases from the `main` branch for production releases
- Use pre-release versions (e.g., `0.8.0-beta.1`) for testing

## Best Practices

1. **Before Release**:
   - Ensure all changes are committed
   - Ensure all tests pass: `dotnet test`
   - Ensure build succeeds: `dotnet build`
   - Update CHANGELOG.md if you have one

2. **Version Numbers**:
   - Increment PATCH for bug fixes (0.7.1 → 0.7.2)
   - Increment MINOR for new features (0.7.2 → 0.8.0)
   - Increment MAJOR for breaking changes (0.8.0 → 1.0.0)

3. **After Release**:
   - Verify GitHub Actions workflow completes successfully
   - Test downloaded artifacts from the release
   - Announce the release (if applicable)
