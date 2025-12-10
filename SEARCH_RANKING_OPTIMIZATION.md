# Search Ranking Optimization

## Overview

SLSKDONET now includes a sophisticated search result ranking system inspired by [slsk-batchdl](https://github.com/fiso64/slsk-batchdl), which implements multi-criteria sorting to intelligently rank search results.

## Features

### 1. **Original Index Tracking**
Every search result now maintains its original position from when the search completed. This allows users to:
- Filter and sort results as desired
- Return to the original search order at any time
- Reference results by their discovery order

**Properties:**
- `OriginalIndex`: The 0-based position in the original search results
- `CurrentRank`: The current ranking score (higher = better match)

### 2. **Advanced Ranking Criteria**

The `ResultSorter` service evaluates results on multiple dimensions:

#### A. Requirement Matching (Highest Priority)
- **Required Conditions**: Does the file pass all required filters?
- **Preferred Conditions Score**: How many preferred conditions does it match? (0.0-1.0)

#### B. Metadata Quality
- **Length Validation**: Is the track duration valid?
- **Length Matching**: How close is the duration to the expected length?
  - Within ±3 seconds: 1.0 (perfect)
  - Within ±6 seconds: 0.75 (good)
  - Within ±12 seconds: 0.5 (acceptable)
  - Beyond ±12 seconds: Scaled down to 0.0

- **Bitrate Matching**: Is it a reasonable quality (≥128 kbps)?
- **Bitrate Value**: Higher bitrate ranks higher (up to 50 point cap)

#### C. String Similarity (Levenshtein Distance)
- **Title Similarity**: How closely does the filename match the search title?
- **Artist Similarity**: Does the filename contain the correct artist?
- **Album Similarity**: Does the filename contain the correct album?

Similarity is calculated as: `1.0 - (levenshtein_distance / max_length)`

Strings are normalized (removed spaces, underscores, dashes) for fairer comparison.

#### D. Tiebreaker
- **Random Tiebreaker**: For identical results, randomization provides variety

### 3. **Overall Scoring**

Results are scored using a weighted point system:

| Criterion | Points | Notes |
|-----------|--------|-------|
| Required Conditions Met | +1000 | Highest weight |
| Preferred Conditions Score | +0-500 | Scales with match count |
| Has Valid Length | +100 | |
| Length Match | +0-100 | Scales with tolerance |
| Bitrate Match | +50 | If ≥128 kbps |
| Bitrate Value | +0-50 | Higher bitrate better |
| Title Similarity | +0-200 | Most important string match |
| Artist Similarity | +0-100 | Medium importance |
| Album Similarity | +0-50 | Lower importance |
| Random Tiebreaker | +0-1 | For variety |

**Example:**
- A 192 kbps MP3 matching format requirements and length precisely: ~1,700 points
- A 320 kbps FLAC with perfect title match: ~2,000+ points
- A 128 kbps MP3 with poor metadata: ~500-700 points

### 4. **Comparison Order**

When sorting results, the system compares in this priority order:

1. Required conditions (pass/fail)
2. Preferred score (0-1)
3. Bitrate match (yes/no)
4. Bitrate value (higher better)
5. Has valid length (yes/no)
6. Length match score (0-1)
7. Title similarity (0-1)
8. Artist similarity (0-1)
9. Album similarity (0-1)
10. Random tiebreaker

## Usage

### In Code

```csharp
// Create file condition evaluator with required/preferred rules
var evaluator = new FileConditionEvaluator();
evaluator.AddRequired(new FormatCondition { AllowedFormats = new List<string> { "mp3", "flac" } });
evaluator.AddPreferred(new LengthCondition { ExpectedLength = 240, ToleranceSeconds = 3 });

// Get the track you're searching for
var searchTrack = new Track { Title = "Get Lucky", Artist = "Daft Punk", Length = 240 };

// Order your results
var searchResults = /* ... get results from search ... */;
var rankedResults = ResultSorter.OrderResults(searchResults, searchTrack, evaluator);

// Access original position if needed
foreach (var result in rankedResults)
{
    Console.WriteLine($"#{result.OriginalIndex}: {result.Artist} - {result.Title} (Score: {result.CurrentRank:F2})");
}
```

### In UI

The GUI automatically:
1. ✅ Displays results in ranked order
2. ✅ Shows original index in grid (if column added)
3. ✅ Preserves original search position internally
4. ✅ Allows filtering while keeping original indices
5. ⏳ Can add "Reset to Original Order" button in future

## Algorithm Details

### Levenshtein Distance Implementation

The Levenshtein distance measures the minimum number of edits (insertions, deletions, substitutions) needed to transform one string into another.

Example:
- "get lucky" → "get luckty" = distance 1
- "daft punk" → "daft pank" = distance 1
- "the beatles" → "beatles" = distance 4

This helps identify results with typos or formatting variations.

### Length Score Calculation

```
If no expected length: score = 0.5 (neutral)
If within ±3 seconds: score = 1.0 (perfect)
If within ±6 seconds: score = 0.75 (good)
If within ±12 seconds: score = 0.5 (acceptable)
Otherwise: score = max(0, 0.25 - diff/1000)
```

This accounts for real-world metadata variations and encoding differences.

## Future Enhancements

1. **User Success Tracking**: Track which users provide high-quality downloads and uprank their results
2. **Queue Length Penalty**: Downrank users with large download queues
3. **Upload Speed Tiers**: Classify users as fast/medium/slow and rank accordingly
4. **Album Mode Optimization**: Special ranking for album downloads
5. **Configurable Weights**: Allow users to adjust ranking weights
6. **Display Original Position**: Show "[Orig: #42]" next to results
7. **Reset to Original Order**: Quick button in UI

## Technical Details

- **Location**: `Services/ResultSorter.cs`
- **Main Class**: `ResultSorter` (static utility) + `SortingCriteria` (IComparable)
- **Inspired By**: [slsk-batchdl ResultSorter.cs](https://github.com/fiso64/slsk-batchdl/blob/main/slsk-batchdl/Services/ResultSorter.cs)
- **Time Complexity**: O(n log n) for sorting + O(m) for each comparison
- **Space Complexity**: O(n) for result storage + O(m) for string operations

## References

- [Levenshtein Distance](https://en.wikipedia.org/wiki/Levenshtein_distance)
- [String Similarity Algorithms](https://en.wikipedia.org/wiki/String_metric)
- [slsk-batchdl Result Sorting](https://github.com/fiso64/slsk-batchdl/blob/main/slsk-batchdl/Services/ResultSorter.cs)
