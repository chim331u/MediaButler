package tokenizer

import (
	"fmt"
	"path/filepath"
	"regexp"
	"strings"
	"unicode"
)

// ItalianTokenizer handles filename tokenization optimized for Italian TV series content
// Ported from .NET TokenizerService.cs with 100% pattern parity
type ItalianTokenizer struct {
	// Episode patterns (6 patterns)
	episodePatterns []*regexp.Regexp

	// Quality patterns (12 patterns)
	qualityPatterns []*regexp.Regexp

	// Language patterns (5 patterns)
	languagePatterns []*regexp.Regexp

	// Release patterns (5 patterns)
	releasePatterns []*regexp.Regexp

	// Utility patterns (2 patterns)
	multipleSpacesPattern    *regexp.Regexp
	releaseGroupPattern      *regexp.Regexp

	// Stop words (Italian-specific)
	stopWords map[string]bool
}

// NewItalianTokenizer creates a new tokenizer instance with all patterns compiled
func NewItalianTokenizer() *ItalianTokenizer {
	t := &ItalianTokenizer{}

	// Episode patterns (tested in order of likelihood for Italian content)
	t.episodePatterns = []*regexp.Regexp{
		regexp.MustCompile(`(?i)(\d{1,2})x(\d{1,2})`),                  // 8x04 (most common in Italian data)
		regexp.MustCompile(`[Ss](\d{1,2})[Ee](\d{1,2})`),               // S01E01 (standard pattern)
		regexp.MustCompile(`(?i)Season\s*(\d{1,2}).*?Episode\s*(\d{1,2})`), // Season 1 Episode 1
		regexp.MustCompile(`[Ee]p?(\d{1,2})`),                          // E01, Ep01 (episode only)
		regexp.MustCompile(`(\d{4})[.\-_](\d{2})[.\-_](\d{2})`),       // Date-based episodes
		regexp.MustCompile(`\b(\d{3,4})\b`),                            // Large episode numbers (One Piece: 1089)
	}

	// Quality patterns (observed in Italian training data)
	t.qualityPatterns = []*regexp.Regexp{
		// Resolution patterns
		regexp.MustCompile(`(?i)\b(2160p|4K|UHD)\b`),          // 4K/UHD
		regexp.MustCompile(`(?i)\b(1080p|FHD)\b`),             // 1080p/Full HD
		regexp.MustCompile(`(?i)\b(720p|HD)\b`),               // 720p/HD
		regexp.MustCompile(`(?i)\b(480p|SD)\b`),               // 480p/SD
		// Source patterns
		regexp.MustCompile(`(?i)\b(WEBMux|WEBDL|WEB-DL|WEB-DLMux)\b`), // Most common in Italian content
		regexp.MustCompile(`(?i)\b(HDTVMux|HDTV)\b`),          // Very common
		regexp.MustCompile(`(?i)\b(DLMux|DL)\b`),              // Italian specific
		regexp.MustCompile(`(?i)\b(BluRay|BDRip|BRRip)\b`),    // Blu-ray sources
		regexp.MustCompile(`(?i)\b(DVDRip|DVD)\b`),            // DVD sources
		// Codec patterns
		regexp.MustCompile(`(?i)\b(x264|H\.264|AVC)\b`),       // x264/H.264
		regexp.MustCompile(`(?i)\b(x265|H\.265|HEVC|h264)\b`), // x265/HEVC (h264 variant)
		regexp.MustCompile(`(?i)\b(XviD|DivX)\b`),             // XviD/DivX
	}

	// Language patterns (Italian content specific)
	t.languagePatterns = []*regexp.Regexp{
		regexp.MustCompile(`(?i)\b(ITA|iTALiAN|ITALIAN)\b`),   // Most common
		regexp.MustCompile(`(?i)\b(ITA_ENG|ENG_ITA)\b`),       // Dual language
		regexp.MustCompile(`(?i)\b(ENG|EN|ENGLISH)\b`),        // English
		regexp.MustCompile(`(?i)\b(SUB|SUBS|SUBTITLES|forced)\b`), // Subtitle indicators
		regexp.MustCompile(`(?i)\b(DUB|DUBBED)\b`),            // Dubbed
	}

	// Release patterns and groups (observed in Italian data)
	t.releasePatterns = []*regexp.Regexp{
		regexp.MustCompile(`(?i)\b(REPACK|PROPER|REAL|FINAL)\b`),      // Repack indicators
		regexp.MustCompile(`(?i)\b(EXTENDED|UNCUT|DIRECTORS?\.CUT)\b`), // Extended versions
		regexp.MustCompile(`(?i)\b(LIMITED|INTERNAL)\b`),              // Limited/Internal
		regexp.MustCompile(`\b(UBi|NovaRip|DarkSideMux|Pir8|iGM)\b`),  // Common Italian groups
		regexp.MustCompile(`-([A-Za-z0-9]+)$`),                        // Generic release group
	}

	// Utility patterns
	t.multipleSpacesPattern = regexp.MustCompile(`\s+`)          // Normalize whitespace
	t.releaseGroupPattern = regexp.MustCompile(`-([A-Za-z0-9]+)(?:\.[a-z]+)?$`) // Extract release group

	// Stop words (very conservative - only technical/noise words)
	t.stopWords = map[string]bool{
		"pack":     true,
		"complete": true,
		"season":   true,
		"serie":    true,
		"series":   true,
		"vol":      true,
		"volume":   true,
		"of":       true, // Common English preposition
	}

	return t
}

// ExtractSeriesName extracts the TV series name from a filename
// This is the core function that mirrors .NET's ExtractSeriesName()
func (t *ItalianTokenizer) ExtractSeriesName(filename string) string {
	if filename == "" {
		return ""
	}

	// Remove file extension
	nameWithoutExt := t.removeExtension(filename)

	// Find episode pattern and extract everything before it
	seriesName := t.extractSeriesNameBeforeEpisode(nameWithoutExt)

	// If no episode pattern found, try to clean the entire filename
	if seriesName == "" {
		seriesName = t.cleanFilenameForSeriesName(nameWithoutExt)
	}

	// Clean and normalize the series name
	cleanedName := t.cleanAndNormalizeSeriesName(seriesName)

	return cleanedName
}

// Tokenize converts a filename into a list of tokens
// Removes quality indicators, language codes, and release info
func (t *ItalianTokenizer) Tokenize(filename string) []string {
	if filename == "" {
		return []string{}
	}

	// Extract series name (already cleaned)
	seriesName := t.ExtractSeriesName(filename)

	// Tokenize the series name
	tokens := t.tokenizeString(seriesName)

	return tokens
}

// extractSeriesNameBeforeEpisode finds the episode marker and extracts text before it
func (t *ItalianTokenizer) extractSeriesNameBeforeEpisode(nameWithoutExt string) string {
	for _, pattern := range t.episodePatterns {
		match := pattern.FindStringIndex(nameWithoutExt)
		if match != nil {
			// Extract everything before the episode pattern
			seriesPart := strings.TrimSpace(nameWithoutExt[:match[0]])
			return seriesPart
		}
	}
	return ""
}

// cleanFilenameForSeriesName removes quality, language, and release patterns
func (t *ItalianTokenizer) cleanFilenameForSeriesName(input string) string {
	cleaned := input

	// Remove quality patterns
	for _, pattern := range t.qualityPatterns {
		cleaned = pattern.ReplaceAllString(cleaned, " ")
	}

	// Remove language patterns
	for _, pattern := range t.languagePatterns {
		cleaned = pattern.ReplaceAllString(cleaned, " ")
	}

	// Remove release patterns
	for _, pattern := range t.releasePatterns {
		cleaned = pattern.ReplaceAllString(cleaned, " ")
	}

	return cleaned
}

// cleanAndNormalizeSeriesName performs final cleaning and normalization
func (t *ItalianTokenizer) cleanAndNormalizeSeriesName(input string) string {
	if strings.TrimSpace(input) == "" {
		return ""
	}

	cleaned := input

	// Replace separators with spaces
	separators := []string{".", "_", "-"}
	for _, sep := range separators {
		cleaned = strings.ReplaceAll(cleaned, sep, " ")
	}

	// Remove multiple spaces and normalize
	cleaned = t.multipleSpacesPattern.ReplaceAllString(cleaned, " ")
	cleaned = strings.TrimSpace(cleaned)

	// Split into words and filter stop words
	words := strings.Fields(cleaned)
	filteredWords := make([]string, 0, len(words))

	for _, word := range words {
		// Skip stop words (case-insensitive)
		if t.stopWords[strings.ToLower(word)] {
			continue
		}
		filteredWords = append(filteredWords, word)
	}

	// Capitalize words (title case with special handling)
	capitalizedWords := make([]string, len(filteredWords))
	for i, word := range filteredWords {
		capitalizedWords[i] = t.capitalizeWord(word)
	}

	result := strings.Join(capitalizedWords, " ")

	// Reasonable length limit (100 chars)
	if len(result) > 100 {
		result = result[:100]
		result = strings.TrimSpace(result)
	}

	return result
}

// capitalizeWord applies title case with special rules
func (t *ItalianTokenizer) capitalizeWord(word string) string {
	if word == "" {
		return word
	}

	// Handle single letters (keep uppercase)
	if len(word) == 1 {
		return strings.ToUpper(word)
	}

	// Italian articles and prepositions (keep lowercase if not first word)
	italianArticles := map[string]bool{
		"di":    true,
		"del":   true,
		"della": true,
		"dello": true,
		"dei":   true,
		"delle": true,
		"of":    true,
		"the":   true,
	}

	if italianArticles[strings.ToLower(word)] {
		return strings.ToLower(word)
	}

	// Preserve all-caps words like NCIS, FBI, CSI (<=4 chars, all uppercase or digits)
	if len(word) <= 4 && t.isAllCapsOrDigits(word) {
		return strings.ToUpper(word)
	}

	// Title case: First letter uppercase, rest lowercase
	runes := []rune(word)
	runes[0] = unicode.ToUpper(runes[0])
	for i := 1; i < len(runes); i++ {
		runes[i] = unicode.ToLower(runes[i])
	}

	return string(runes)
}

// isAllCapsOrDigits checks if a word is all uppercase letters or digits
func (t *ItalianTokenizer) isAllCapsOrDigits(word string) bool {
	for _, r := range word {
		if !unicode.IsUpper(r) && !unicode.IsDigit(r) {
			return false
		}
	}
	return true
}

// tokenizeString splits a string into tokens (words)
func (t *ItalianTokenizer) tokenizeString(input string) []string {
	if input == "" {
		return []string{}
	}

	// Split on whitespace
	tokens := strings.Fields(input)

	// Convert to lowercase for ML processing
	lowercaseTokens := make([]string, len(tokens))
	for i, token := range tokens {
		lowercaseTokens[i] = strings.ToLower(token)
	}

	return lowercaseTokens
}

// removeExtension removes file extension from filename
func (t *ItalianTokenizer) removeExtension(filename string) string {
	// Get just the filename (not the full path)
	base := filepath.Base(filename)

	// Remove extension
	ext := filepath.Ext(base)
	if ext != "" {
		return base[:len(base)-len(ext)]
	}

	return base
}

// ExtractEpisodeInfo extracts episode and season numbers
func (t *ItalianTokenizer) ExtractEpisodeInfo(filename string) (season, episode int, found bool) {
	nameWithoutExt := t.removeExtension(filename)

	for _, pattern := range t.episodePatterns {
		matches := pattern.FindStringSubmatch(nameWithoutExt)
		if len(matches) >= 3 {
			// Try to parse season and episode
			var s, e int
			_, err1 := fmt.Sscanf(matches[1], "%d", &s)
			_, err2 := fmt.Sscanf(matches[2], "%d", &e)

			if err1 == nil && err2 == nil {
				return s, e, true
			}
		}
	}

	return 0, 0, false
}

// ExtractReleaseGroup extracts the release group from filename
func (t *ItalianTokenizer) ExtractReleaseGroup(filename string) string {
	nameWithoutExt := t.removeExtension(filename)

	matches := t.releaseGroupPattern.FindStringSubmatch(nameWithoutExt)
	if len(matches) >= 2 {
		return matches[1]
	}

	return ""
}
