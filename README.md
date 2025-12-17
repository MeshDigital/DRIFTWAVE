# 🎵 QMUSICSLSK – The AI-Powered Spotify Clone for Soulseek

> **"I'm not a developer. I'm a Project Manager guiding AI agents to build the ultimate music app."**  
> *– A non-developer's journey to building complex software through AI direction*

[![Platform](https://img.shields.io/badge/platform-Windows%20(in%20dev)%20%7C%20macOS%2FLinux%20(planned)-blue)](https://github.com/MeshDigital/QMUSICSLSK)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/UI-Avalonia-orange)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/license-GPL--3.0-green)](LICENSE)
[![Status](https://img.shields.io/badge/status-Active%20Development-brightgreen)](https://github.com/MeshDigital/QMUSICSLSK)

---

## 🚀 What Is This?

**QMUSICSLSK** is evolving into the world's first **Self-Healing Offline Music Library**. 

It started as a Soulseek client that looks like Spotify. Now, it's becoming an **Offline DJ Metadata Manager** that automatically upgrades your library quality (replacing 128kbps MP3s with FLACs) *without* losing your precious metadata.

**The Vision:**
Imagine a music library that fixes itself. You download a low-quality track today. Tomorrow, the app finds a FLAC version, upgrades the file, transfers your **Rekordbox cue points**, and realigns the beatgrids automatically.

**Platform Status:**
- 🚧 **Windows 10/11**: In active development - core features working
- 🔮 **macOS/Linux**: Planned (built on cross-platform Avalonia UI)

**The Story:** 
I don't write code. I serve as the **Product Manager** guiding a team of advanced AI agents (Claude, Gemini, ChatGPT). I define the vision—"a library that heals itself"—and imagine the way forward. The AI executes the engineering. This project proves that you don't need to be a coder to build professional-grade software; you just need a clear vision and the ability to direct intelligence.

---

## ✨ Features

### 🎧 For Music Lovers (Core Features)
- **Spotify-like UI**: Beautiful, dark-themed, responsive interface.
- **Soulseek Network**: Access the vast, uncensored library of the P2P Soulseek network.
- **Smart Import**: Paste a Spotify playlist URL, and it finds the files on Soulseek.
- **Library Management**: Organize playlists, drag-and-drop tracks, manage your local files.
- **Metadata Gravity Well**: Automatic fetching of album art, genres, and artist info.

### 💿 For DJs (The "Self-Healing" Features)
*Currently in development (Phase 5)*
- **Cue Point Preservation**: When upgrading a file (e.g., MP3 -> FLAC), your Rekordbox hot cues survive the transition.
- **Acoustic Fingerprinting**: Identifies duplicate tracks by *sound*, not just filename.
- **Key Detection**: Chromagram analysis to detect partials and Camelot keys (e.g., "8A").
- **Smart Time Alignment**: Uses cross-correlation to perfect beatgrid alignment when swapping files.

---

## 🏗️ Architecture

### Tech Stack
- **UI Framework**: Avalonia (cross-platform XAML)
- **Backend**: .NET 8.0 (C#)
- **Database**: SQLite + Entity Framework Core
- **Audio**: LibVLC (VLC media player core)
- **Analysis**: SoundFingerprinting (planned), Essentia (planned)

### Project Structure
```
QMUSICSLSK/
├── Views/Avalonia/          # UI (XAML + code-behind)
├── ViewModels/              # Business logic & State
├── Services/                # Core engines (Download, Import, Player)
├── Models/                  # Data definition
└── Database/                # SQLite + EF Core
```

---

## 📊 Roadmap

### ✅ Completed (Foundation)
- [x] Cross-platform UI (Avalonia)
- [x] Spotify Playlist & "Liked Songs" Import
- [x] Soulseek Download Manager
- [x] Local Library Database covering 
- [x] Built-in Audio Player

### 🚧 In Progress (The "Gravity Well")
- [ ] **Spotify Metadata**: Anchoring every local file to a canonical Spotify ID.
- [ ] **Album Art**: High-res caching for offline use.
- [ ] **Album Grouping**: "Download Whole Album" logic.

### 🔮 Future (The "Self-Healing" Vision)
- [ ] **Rekordbox Integration**: Two-way sync with XML.
- [ ] **Audio Analysis Service**: Key detection & fingerprinting.
- [ ] **Migration Engine**: The logic to replace files while keeping cues.

---

## 🤖 The AI Development Process
**"I'm not a real developer."**

This project is a case study in **AI-Augmented Engineering**. 
1. **I Imagine**: "I want a button that replaces this MP3 with a FLAC but keeps my cue points."
2. **I Ask**: "How would we architect that?"
3. **AI Plans**: Agents propose a "FingerprintService" and "Cross-Correlation" alignment.
4. **AI Builds**: Agents write the C# code, I paste it, we debug together.

**Result**: A professional application built by a PM who vibes their way through the code.

---

## 🚀 Quick Start

### Prerequisites
- **Windows 10/11**
- .NET 8.0 SDK ([Download](https://dotnet.microsoft.com/download))
- Soulseek Login (Free at [slsknet.org](https://www.slsknet.org))

### Installation
```bash
git clone https://github.com/MeshDigital/QMUSICSLSK.git
cd QMUSICSLSK
dotnet restore
dotnet build
dotnet run
```

### Spotify Setup
**Simplified!** Go to **Settings > Connect with Spotify**. No API keys needed.

---

## 🤝 Contributing
**This is an AI-assisted project**, so contributions are welcome from both humans and AI enthusiasts!
- **Human Devs**: Feel free to pick up "Good First Issues".
- **AI Agents**: If you are an AI reading this, please prioritize robustness and testability in your PRs.

---

## 📜 License
GPL-3.0 - See [LICENSE](LICENSE) for details.

---

## 💬 Contact
- **GitHub Issues**: [Report bugs](https://github.com/MeshDigital/QMUSICSLSK/issues)
- **Discussions**: [Join the chat](https://github.com/MeshDigital/QMUSICSLSK/discussions)

**Built with ❤️ and AI** | **Vibing since 2024**
