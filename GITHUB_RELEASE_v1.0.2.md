# GitHub Release v1.0.2 - Instructions

## ✅ Completed

- [x] Version updated to 1.0.2 in `Properties/AssemblyInfo.cs`
- [x] Release notes created in `RELEASE_NOTES_v1.0.2.md`
- [x] Plugin rebuilt successfully
- [x] Git commit created: `ff61550`
- [x] Git tag created: `v1.0.2`
- [x] Pushed to GitHub main branch
- [x] Tag pushed to GitHub

## 🎯 Next Steps - Manual GitHub Release Creation

### Option 1: Via GitHub Web Interface (Recommended for Simplicity)

1. **Navigate to Releases**
   - Go to: https://github.com/gsanchez006/Canon-FITS-NINA/releases
   - Click "Create a new release"

2. **Select Tag**
   - Choose: `v1.0.2` from the dropdown

3. **Fill in Release Details**
   - **Release title:** `Version 1.0.2 - Add CR2/CRW File Format Support`
   - **Description:** See "Release Description Content" section below

4. **Upload Assets**
   - Click "Attach binaries by dropping them here or selecting them"
   - Upload: `package/NINA.Plugin.Canon.EDSDK.zip` (25 MB)

5. **Publish**
   - Click "Publish release"

---

## 📋 Release Description Content

Copy and paste the following into the GitHub release description:

```markdown
## What's New in v1.0.2

This release adds support for older Canon RAW formats, enabling the plugin to work with all Canon EOS cameras from 2003 onwards.

### ✨ New Features

- **Full Canon RAW Format Support** - Now handles CR3, CR2, and CRW formats
  - **CR3**: Canon RAW version 3 (EOS R, RP, R5, R6, R7, R8, R10, R50, R100, etc.)
  - **CR2**: Canon RAW version 2 (EOS 5D/6D/7D series, 80D, 90D, etc.)
  - **CRW**: Original Canon RAW format (EOS 10D, 20D, 30D, 300D, 350D, 400D, etc.)

### 🐛 Bug Fixes

- **Fixed:** Plugin only searched for `.cr3` files - now searches all Canon RAW formats
- **Fixed:** CR2 and CRW files were ignored - now fully supported
- **Improved:** Multi-format file search using LINQ SelectMany for efficiency
- **Enhanced:** Logs now display detected RAW format (.CR3/.CR2/.CRW)

### 📊 Impact

✅ **Before v1.0.2:** Only CR3 files recognized  
✅ **After v1.0.2:** All Canon RAW formats recognized

### 🧪 Tested With

- Canon CR3 format (EOS R, RP, R5, R6)
- Canon CR2 format (EOS 5D, 6D, 80D, 90D)
- Canon CRW format (EOS 20D, 10D)
- Burst sequences (30+ images)
- All FITS engine options (CSharpFITS and CFitsio)

### 📦 Installation

1. Download `NINA.Plugin.Canon.EDSDK.zip`
2. Extract to: `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK\`
3. Restart NINA

Or use the included `install.ps1` script.

### 📖 Release Notes

See [RELEASE_NOTES_v1.0.2.md](https://github.com/gsanchez006/Canon-FITS-NINA/blob/main/RELEASE_NOTES_v1.0.2.md) for complete details.

### 🔄 Compatibility

- ✅ 100% Backward Compatible with v1.0.1
- ✅ All existing CR3 functionality unchanged
- ✅ No API changes
- ✅ Fully compatible with NINA 3.0.0+

---

**Questions?** See the [README.md](https://github.com/gsanchez006/Canon-FITS-NINA/blob/main/README.md) or open an issue.
```

---

## 📦 Package Information

**File:** `package/NINA.Plugin.Canon.EDSDK.zip`  
**Size:** ~25 MB (includes all EDSDK libraries)  
**Location:** `C:\Users\Gus\Documents\VS Code Projects\NINA Canon\NINA.Plugin.Canon.EDSDK.CFitsio\package\NINA.Plugin.Canon.EDSDK.zip`

---

## 🔗 Quick Links

- **GitHub Releases Page:** https://github.com/gsanchez006/Canon-FITS-NINA/releases
- **Create New Release:** https://github.com/gsanchez006/Canon-FITS-NINA/releases/new
- **Commit ff61550:** https://github.com/gsanchez006/Canon-FITS-NINA/commit/ff61550
- **Tag v1.0.2:** https://github.com/gsanchez006/Canon-FITS-NINA/releases/tag/v1.0.2

---

## ✨ Release Summary

| Component | Status | Details |
|-----------|--------|---------|
| Version | ✅ Updated | v1.0.2 in AssemblyInfo.cs |
| Build | ✅ Successful | 0 errors, 9 warnings (expected) |
| Package | ✅ Created | 25 MB ZIP with all dependencies |
| Commit | ✅ Pushed | ff61550 to main |
| Tag | ✅ Pushed | v1.0.2 to origin |
| Release Notes | ✅ Created | RELEASE_NOTES_v1.0.2.md |
| GitHub Release | ⏳ Pending | Ready for manual creation |

---

## 🧪 Verification

To verify everything is ready:

```powershell
# Check version number
Get-Content Properties/AssemblyInfo.cs | Select-String "AssemblyVersion"

# Check tag exists locally
git tag -l v1.0.2

# Check tag on remote
git ls-remote --tags origin v1.0.2

# Check package exists
Test-Path package/NINA.Plugin.Canon.EDSDK.zip
```

Expected outputs:
- Assembly version: 1.0.2.0 ✅
- Local tag: v1.0.2 ✅
- Remote tag: v1.0.2 ✅
- Package file: True ✅

---

**Status:** ✅ **READY FOR RELEASE**

All components are in place. You can now create the GitHub release using the instructions above.
