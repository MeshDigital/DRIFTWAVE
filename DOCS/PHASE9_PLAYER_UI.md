# Phase 9: Media Player UI Polish

**Status**: Planning Complete, Awaiting Implementation  
**Priority**: HIGH (User-facing)  
**Estimated Time**: 2.5-3 hours  
**Owner**: Development Team

---

## 📋 Overview

Comprehensive fix and optimization of the media player controls in `PlayerControl.axaml` to improve visual consistency, error handling, and user experience.

## 🎯 Objectives

1. **Fix Critical Issues** - Ensure all converters are registered and commands work correctly
2. **Improve Visual Feedback** - Add loading/error states for better user awareness
3. **Enhance UX** - Implement like button, keyboard shortcuts, album artwork

## 📦 Deliverables

### Phase 9.1: Critical Fixes (30-45 min)
- ✅ Verify `BoolToPlayPauseIconConverter` exists (check `BooleanConverters.cs`)
- ✅ Verify `RepeatModeIconConverter` exists
- ✅ Register all converters in `PlayerControl.axaml` Resources
- ✅ Fix `PlayPauseCommand` vs `TogglePlayPauseCommand` binding
- ✅ Test all button functionality

**Files**: `PlayerControl.axaml`, `Converters/BooleanConverters.cs`

### Phase 9.2: Visual Improvements (1h)
- ✅ Add `IsLoading` property to `PlayerViewModel`
- ✅ Add loading spinner UI during track load
- ✅ Add `HasPlaybackError` and `PlaybackError` properties
- ✅ Add error banner UI for playback failures
- ✅ Add `AlbumArtUrl` property to ViewModel
- ✅ Update album art section to show dynamic artwork

**Files**: `PlayerViewModel.cs`, `PlayerControl.axaml`

### Phase 9.3: UX Enhancements (45min-1h)
- ✅ Implement `ToggleLikeCommand` in ViewModel
- ✅ Add `IsCurrentTrackLiked` property
- ✅ Wire up Like button in UI
- ✅ Add keyboard shortcuts in `PlayerControl.axaml.cs`:
  - **Space**: Play/Pause
  - **Right Arrow**: Next track
  - **Left Arrow**: Previous track
- ✅ Add hover animations to buttons

**Files**: `PlayerViewModel.cs`, `PlayerControl.axaml`, `PlayerControl.axaml.cs`

### Phase 9.4: Optional Polish (30min)
- ⚠️ Replace emoji icons with Path geometries (cross-platform consistency)
- ⚠️ Add context menu to queue items
- ⚠️ Add micro-animations for button interactions

**Status**: Optional, user approval required

---

## 🔧 Technical Details

### Current Issues Identified

#### Missing Converter Registration
PlayerControl.axaml references 4 converters that may not be in Resources:
- `BoolToPlayPauseIconConverter` (line 128)
- `BoolToColorConverter` (line 167)
- `RepeatModeIconConverter` (line 181)
- `RepeatModeColorConverter` (line 183)

**Good News**: Found 10 existing converter files in `Views/Avalonia/Converters/`

#### Command Binding Mismatch
Line 116: `Command="{Binding PlayPauseCommand}"`  
ViewModel Line 147: `public ICommand TogglePlayPauseCommand { get; }`

**Fix**: Update XAML to use `TogglePlayPauseCommand`

#### Missing Functionality
1. Like button has no command binding (line 218)
2. No loading/error states in UI
3. Album artwork property exists but not displayed
4. No keyboard shortcut support

---

## 📊 Success Metrics

- [ ] All player buttons functional
- [ ] Play/Pause icon toggles correctly
- [ ] Shuffle/Repeat colors change on toggle
- [ ] Loading spinner shows during track load
- [ ] Error banner displays on playback failure
- [ ] Album artwork loads dynamically
- [ ] Keyboard shortcuts work (Space, arrows)
- [ ] Like button saves state to database

---

## 🧪 Testing Plan

### Manual Testing
1. **Playback Controls**
   - Click Play → verify playback starts
   - Click Pause → verify playback pauses
   - Click Next → verify next track plays
   - Click Previous → verify previous track plays

2. **Shuffle & Repeat**
   - Toggle Shuffle → verify icon color changes
   - Toggle Repeat → verify cycles through Off/All/One
   - Play with Shuffle ON → verify random order
   - Play with Repeat One → verify track repeats

3. **Visual States**
   - Load a track → verify loading spinner appears/disappears
   - Trigger playback error → verify error banner shows
   - Load track with artwork → verify image displays
   - Load track without artwork → verify fallback icon shows

4. **Keyboard Shortcuts**
   - Press Space → verify play/pause toggles
   - Press Right Arrow → verify next track
   - Press Left Arrow → verify previous track

5. **Like Button**
   - Click Like → verify state persists after restart

### Edge Cases
- [ ] Empty queue behavior
- [ ] Last track in queue with Repeat OFF
- [ ] Shuffle with only 1 track
- [ ] Network timeout during track load
- [ ] Missing album artwork URL

---

## 📁 Files Modified

| File | Lines Changed | Type |
|------|---------------|------|
| `ViewModels/PlayerViewModel.cs` | +40 | C# |
| `Views/Avalonia/PlayerControl.axaml` | ~50 | XAML |
| `Views/Avalonia/PlayerControl.axaml.cs` | +30 | C# |
| `Views/Avalonia/Converters/BooleanConverters.cs` | +30 (if needed) | C# |

**Total**: ~150 lines

---

## 🚀 Implementation Checklist

- [ ] **Phase 9.1**: Critical fixes (converters, commands)
- [ ] **Phase 9.2**: Visual improvements (loading, errors, artwork)
- [ ] **Phase 9.3**: UX enhancements (like, keyboard, animations)
- [ ] **Phase 9.4**: Optional polish (Path icons, context menus)
- [ ] **Testing**: All manual tests passing
- [ ] **Documentation**: Update CHANGELOG.md
- [ ] **User Review**: Get feedback on improvements

---

## 📚 Related Documents

- [Player UI Fix Plan (Detailed)](file:///C:/Users/quint/.gemini/antigravity/brain/25e4bde4-69b6-47ac-9781-9724e2c1975d/player_ui_fix_plan.md)
- [ROADMAP.md](../ROADMAP.md)
- [PlayerViewModel.cs](../ViewModels/PlayerViewModel.cs)
- [PlayerControl.axaml](../Views/Avalonia/PlayerControl.axaml)

---

**Last Updated**: 2025-12-18  
**Status**: Ready for Implementation
