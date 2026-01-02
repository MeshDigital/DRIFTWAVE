# Search 2.0: Curation Assistant & Metadata Intelligence

## 🎯 Objective
To transform the Search experience from a raw list of files into a **Curation Assistant** that actively helps the user identify high-quality, authentic files *before* downloading.

Since we cannot analyze audio content before download (P2P limitation), we use **Metadata Intelligence**—probabilistic heuristics based on file properties.

---

## 🧠 Metadata Intelligence (The "Brain")

### 1. Fake Detection Heuristics
We will implement a `MetadataForensicEngine` to flag suspicious files.

#### A. The "Compression Mismatch" Rule
Detects files claimed to be high bitrate (e.g., 320kbps) but are mathematically too small.
- **Formula**: `ExpectedSize = (Bitrate * Duration) / 8`
- **Threshold**: If `ActualSize < (ExpectedSize * 0.75)`, flag as **Suspicious**.
    - *Example*: A 5-minute 320kbps MP3 should be ~12MB. If a search result says "320kbps" but is 3MB, it is a fake (likely 64kbps upscaled).

#### B. The "Variable Bitrate" (VBR) Validator
VBR files report an "Average Bitrate".
- If `Bitrate == 128` (CBR) but filename contains `[V0]` or `[V2]`, flag as **Mislabelled**.
- Trust `V0` (High Quality) and `320` (CBR) over generic inputs.

#### C. Extension Trust
- **High Trust**: `.flac`, `.wav`, `.aiff` (Lossless) - *Verify size corresponds to lossless (~10MB/min)*.
- **Standard**: `.mp3`, `.m4a`
- **Low Trust**: `.wma`, `.ogg` (often lower quality in P2P networks).

### 2. Trust Score (0-100)
A composite score calculated for every search result:
- **Base**: 50 points
- **Bitrate**: +10 for 320kbps/V0, +20 for FLAC.
- **Availability**: +10 if Free Speed > 0.
- **Heuristic Check**: -50 if "Compression Mismatch" detected.
- **Integrity**: +10 if duration allows for validation.

---

## 🎨 UI: The Curation Assistant

### 1. Visual Hierarchy
Rows are no longer uniform.
- **🥇 Golden Match (90+ Score)**:
    - Gold border/glow.
    - "VERIFIED QUALITY" badge.
    - Always strictly 320kbps/FLAC with correct size math.
- **🥈 Silver Match (70-89 Score)**:
    - Standard high-quality results.
- **⚠️ Suspicious (Red Flag)**:
    - Dimmed opacity.
    - Warning icon: "Size mismatch: Likely Fake".

### 2. The "Brain" Panel
Replacing tooltips with a structured detail view (Side Panel or Expandable Row).
- **Bars**: Visualizing Bitrate, Speed, and Trust.
- **Forensic Check**:
    - ✅ Duration Matches Size
    - ✅ Extension Trusted
    - ❌ Bitrate Suspicious

### 3. Smart Filters
- **"Hide Fakes"**: Automatically remove results with Trust Score < 40.
- **"DJ Ready"**: Only show 320kbps/FLAC with high availability.

---

## 🛠️ Implementation Plan

### Phase 12.1: The Forensic Engine
- Create `MetadataForensicService`.
- Implement `CalculateTrustScore(SearchResult result)`.

### Phase 12.2: Search Result Visuals
- Update `SearchResultViewModel` with `TrustScore` and `ForensicAssessment`.
- Create `SearchRowTemplate` with Badges and Visual Hierarchy.

### Phase 12.3: The Assistant UI
- Add "Filter & Sort" sidebar with "Smart Filter" toggles.
