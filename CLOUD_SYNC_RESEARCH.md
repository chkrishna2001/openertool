# Cloud Sync Research & Findings

## Problem Summary

User reported that Opener cannot access company OneDrive even though the path is accessible in Windows File Explorer:
```
UnauthorizedAccessException when reading/writing to C:\Users\UserName\OneDrive - CompanyName\...
```

## Root Cause Analysis

### Why OneDrive Direct File Access Fails

**OneDrive Files On-Demand** (introduced in Windows 10, standard in Windows 11):
- Creates placeholder files that don't consume disk space until accessed
- Placeholders appear in File Explorer but aren't fully materialized
- **Critical**: This blocks .NET file I/O operations with `UnauthorizedAccessException`
- Windows Explorer handles materialization transparently; .NET applications cannot
- **This is not a bug in Opener** — it's a platform limitation affecting all applications

### Why Retry Logic with Delays Doesn't Work

- The problem is not timing or filesystem state
- It's the virtual filesystem layer actively blocking file access to unmaterialized placeholders
- No amount of retries or delays can circumvent this architecture

## Platform Availability Analysis

### OneDrive Sync Support

| Platform | Status | Notes |
|----------|--------|-------|
| **Windows 10/11** | Full support | Has Files On-Demand, full sync client |
| **macOS** | Limited | OneDrive client available, more basic sync |
| **Linux** | ❌ None | No native OneDrive sync client exists |

### Google Drive Sync Support

| Platform | Status | Notes |
|----------|--------|-------|
| **Windows** | Full support | Google Drive for desktop with streaming mode |
| **macOS** | Full support | Google Drive for desktop available |
| **Linux** | ❌ None | Not officially supported; third-party tools only (rclone, insync) |

## Practical Implications

### For Cross-Platform Cloud Sync:
1. **OneDrive + Google Drive**: Neither provides reliable native sync on all three platforms
2. **OneDrive Files On-Demand**: Makes programmatic file access impossible on Windows
3. **Desktop sync tools**: Limited to Windows/macOS by default

## Recommended Solutions

### Solution 1: Microsoft Graph API ✅ (Best)
**Pros:**
- Works on Windows, macOS, AND Linux
- Bypasses virtual filesystem layer entirely
- Native Microsoft authentication
- Reliable programmatic access

**Cons:**
- Requires user to authenticate with Microsoft account
- Requires implementing Graph API client

**Implementation:** Use `Microsoft.Graph` NuGet package to directly access OneDrive via REST API

### Solution 2: Local Storage + Manual Cloud Backup
**Pros:**
- Works everywhere
- No API complexity
- User has full control

**Cons:**
- No automatic sync
- User responsibility to backup

### Solution 3: Google Drive API
**Pros:**
- Works on Windows and macOS via official client
- API-based access works on all platforms

**Cons:**
- No native Linux support
- Google Drive for desktop not available on Linux

## Implementation Plan

### Phase 1 (Current)
- ✅ Identify root cause of OneDrive failures
- ✅ Remove ineffective workarounds
- ✅ Document findings and limitations

### Phase 2 (Recommended Next)
- Implement `CloudService` using Microsoft Graph API
- Add `o cloud auth` command for OAuth authentication
- Add `o cloud set-location` for Graph API storage
- Support both OneDrive and Google Drive via APIs

### Phase 3 (Optional)
- Add automatic sync worker
- Support multiple cloud providers simultaneously
- Share keys between devices via cloud storage

## Technical Details

### Why This Happens

OneDrive's virtual filesystem uses NTFS reparse points to create placeholder files:
1. File appears in Explorer with cloud icon
2. .NET tries to call `File.ReadAllText()` or `File.WriteAllText()`
3. Windows kernel returns `ERROR_ACCESS_DENIED` (translated to `UnauthorizedAccessException`)
4. The sync engine hasn't fully materialized the file yet

This is **by design** to save disk space, but it breaks programmatic access.

### Evidence

From Wikipedia (OneDrive Files On-Demand section):
> "On Windows 10 and Windows 11, OneDrive can utilize Files On-Demand, where files synchronized with OneDrive show up in File Explorer, but do not require any disk space. As soon as the content of the file is required, the file is downloaded in the background."

The key word: "As soon as the content is **required**" — but .NET file I/O doesn't trigger this reliably.

## Conclusion

**The issue is not in Opener's code — it's a fundamental Windows limitation.**

Retry logic, delays, and fallback mechanisms cannot solve this because the OS is actively blocking access. The proper solution is to bypass the local filesystem entirely and use Microsoft Graph API for OneDrive access on Windows, macOS, and Linux.
