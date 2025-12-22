# Current Issues Found

## 1. Spotify API 403 Forbidden Errors ❌

**Error:** `Insufficient client scope` when calling `GetAudioFeaturesBatchAsync`

**Root Cause:** Your Spotify OAuth token was created without the `user-read-playback-position` scope required for audio features API.

**Fix Options:**
- **Option A (Quick):** Disable Spotify API usage in Settings → uncheck "Use Spotify API"
- **Option B (Proper):** Re-authenticate with Spotify to get new scopes:
  1. Go to Settings
  2. Click "Disconnect" from Spotify
  3. Click "Connect" again (this will request proper scopes)

**Impact:** Audio features (BPM, Energy, Valence) won't be fetched until fixed. Downloads still work.

---

## 2. Downloads Page Shows Nothing ❌

**Issue:** Downloads are triggering but not visible in UI

**Root Cause:** `DownloadsPage.axaml` has NO `ItemsSource` bindings to display downloads.

**Evidence from logs:**
```
[00:13:40 INF] Processing DownloadAlbumRequest for Project: WHOAB2025
[00:13:40 INF] Loading tracks for project...
[00:13:40 WRN] No tracks found for project
```

**Fix Required:** Add ItemsControl with proper bindings to DownloadCenterViewModel

---

## 3. Track Loading Fixed ✅

**Status:** RESOLVED
- Made `Format` property nullable
- Tracks now load correctly (186 tracks loaded for WHOAB2025)

---

## Recommended Actions

1. **Immediate:** Disable Spotify API in Settings to stop the 403 spam
2. **Fix Downloads UI:** Add ItemsSource bindings to DownloadsPage.axaml
3. **Optional:** Re-auth Spotify with proper scopes later

Would you like me to:
- A) Fix the Downloads page UI to show active downloads?
- B) Add a Settings toggle to disable Spotify enrichment?
- C) Both?
