package tokenizer

import (
	"reflect"
	"testing"
)

// TestExtractSeriesName_StandardFormat tests S##E## format
func TestExtractSeriesName_StandardFormat(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name     string
		filename string
		want     string
	}{
		{
			name:     "Standard S01E01 format",
			filename: "Breaking.Bad.S01E01.mkv",
			want:     "Breaking Bad",
		},
		{
			name:     "Multiple words with S##E## format",
			filename: "The.Walking.Dead.S11E24.FINAL.ITA.ENG.1080p.mkv",
			want:     "The Walking Dead",
		},
		{
			name:     "Game of Thrones Italian",
			filename: "Il Trono Di Spade 8x04 L Ultimo Degli Stark ITA WEBMux x264-UBi mkv.mkv",
			want:     "Il Trono Di Spade",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tokenizer.ExtractSeriesName(tt.filename)
			if got != tt.want {
				t.Errorf("ExtractSeriesName() = %v, want %v", got, tt.want)
			}
		})
	}
}

// TestExtractSeriesName_ItalianFormat tests #x## format (most common in Italian data)
func TestExtractSeriesName_ItalianFormat(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name     string
		filename string
		want     string
	}{
		{
			name:     "Italian 8x04 format",
			filename: "The.Walking.Dead.8x04.ITA.mkv",
			want:     "The Walking Dead",
		},
		{
			name:     "One Piece with large episode number",
			filename: "[Trash] One.Piece.1089.1080p.mkv",
			want:     "Trash One Piece",
		},
		{
			name:     "Attack on Titan Italian",
			filename: "L Attacco Dei Giganti 3x02 Pain iTA AC3 WEBMux x264-ADE CreW mkv.mkv",
			want:     "L Attacco Dei Giganti",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tokenizer.ExtractSeriesName(tt.filename)
			if got != tt.want {
				t.Errorf("ExtractSeriesName() = %v, want %v", got, tt.want)
			}
		})
	}
}

// TestExtractSeriesName_DateBased tests date-based episode naming
func TestExtractSeriesName_DateBased(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name     string
		filename string
		want     string
	}{
		{
			name:     "Date-based episode",
			filename: "Series.2023-01-15.1080p.mkv",
			want:     "Series",
		},
		{
			name:     "Date with dots",
			filename: "Show.Name.2023.12.25.WEBMux.mkv",
			want:     "Show Name",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tokenizer.ExtractSeriesName(tt.filename)
			if got != tt.want {
				t.Errorf("ExtractSeriesName() = %v, want %v", got, tt.want)
			}
		})
	}
}

// TestTokenize_RemovesQualityIndicators tests quality indicator removal
func TestTokenize_RemovesQualityIndicators(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name     string
		filename string
		want     []string
	}{
		{
			name:     "Removes 1080p",
			filename: "Series.Name.1080p.mkv",
			wantContains:    false,
			wantValue:       "1080p",
		},
		{
			name:     "Removes BluRay",
			filename: "Series.Name.BluRay.mkv",
			wantContains:    false,
			wantValue:       "bluray",
		},
		{
			name:     "Removes x264",
			filename: "Series.Name.x264.mkv",
			wantContains:    false,
			wantValue:       "x264",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tokens := tokenizer.Tokenize(tt.filename)
			contains := false
			for _, token := range tokens {
				if token == tt.wantValue {
					contains = true
					break
				}
			}
			if contains == tt.wantContains {
				t.Errorf("Tokenize() tokens = %v, should not contain %v", tokens, tt.wantValue)
			}
		})
	}
}

// TestTokenize_RemovesLanguageCodes tests language code removal
func TestTokenize_RemovesLanguageCodes(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	filename := "Series.Name.ITA.ENG.SUB.mkv"
	tokens := tokenizer.Tokenize(filename)

	// Tokens should not contain language codes
	unwanted := []string{"ita", "eng", "sub"}
	for _, unwantedToken := range unwanted {
		for _, token := range tokens {
			if token == unwantedToken {
				t.Errorf("Tokenize() = %v, should not contain %v", tokens, unwantedToken)
			}
		}
	}
}

// TestTokenize_RemovesReleaseGroups tests release group removal
func TestTokenize_RemovesReleaseGroups(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name     string
		filename string
	}{
		{
			name:     "UBi release group",
			filename: "Series.Name-UBi.mkv",
		},
		{
			name:     "NovaRip release group",
			filename: "Series.Name-NovaRip.mkv",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tokens := tokenizer.Tokenize(tt.filename)

			// Tokens should not contain release group indicators
			unwanted := []string{"ubi", "novarip", "darksid emux"}
			for _, unwantedToken := range unwanted {
				for _, token := range tokens {
					if token == unwantedToken {
						t.Errorf("Tokenize() = %v, should not contain %v", tokens, unwantedToken)
					}
				}
			}
		})
	}
}

// TestTokenize_RealWorldItalianFilenames tests against real Italian training data
func TestTokenize_RealWorldItalianFilenames(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name     string
		filename string
		want     []string
	}{
		{
			name:     "Game of Thrones Italian",
			filename: "Il Trono Di Spade 8x04 L Ultimo Degli Stark ITA WEBMux x264-UBi mkv.mkv",
			want:     []string{"il", "trono", "di", "spade"},
		},
		{
			name:     "Attack on Titan Italian",
			filename: "L Attacco Dei Giganti 3x02 Pain iTA AC3 WEBMux x264-ADE CreW mkv.mkv",
			want:     []string{"l", "attacco", "dei", "giganti"},
		},
		{
			name:     "Bones",
			filename: "Bones.8x06.Il.Patriota.Nel.Bunker.iTALiAN.HDTVMux-DarkSideMux.mkv",
			want:     []string{"bones"},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tokenizer.Tokenize(tt.filename)
			if !reflect.DeepEqual(got, tt.want) {
				t.Errorf("Tokenize() = %v, want %v", got, tt.want)
			}
		})
	}
}

// TestExtractEpisodeInfo tests episode information extraction
func TestExtractEpisodeInfo(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name        string
		filename    string
		wantSeason  int
		wantEpisode int
		wantFound   bool
	}{
		{
			name:        "Standard S01E01",
			filename:    "Series.S01E01.mkv",
			wantSeason:  1,
			wantEpisode: 1,
			wantFound:   true,
		},
		{
			name:        "Italian 8x04 format",
			filename:    "Series.8x04.mkv",
			wantSeason:  8,
			wantEpisode: 4,
			wantFound:   true,
		},
		{
			name:        "No episode info",
			filename:    "Movie.Name.2023.mkv",
			wantSeason:  0,
			wantEpisode: 0,
			wantFound:   false,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			season, episode, found := tokenizer.ExtractEpisodeInfo(tt.filename)
			if season != tt.wantSeason || episode != tt.wantEpisode || found != tt.wantFound {
				t.Errorf("ExtractEpisodeInfo() = (%v, %v, %v), want (%v, %v, %v)",
					season, episode, found, tt.wantSeason, tt.wantEpisode, tt.wantFound)
			}
		})
	}
}

// TestExtractReleaseGroup tests release group extraction
func TestExtractReleaseGroup(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name     string
		filename string
		want     string
	}{
		{
			name:     "UBi group",
			filename: "Series.Name.S01E01-UBi.mkv",
			want:     "UBi",
		},
		{
			name:     "NovaRip group",
			filename: "Series.Name.S01E01-NovaRip.mkv",
			want:     "NovaRip",
		},
		{
			name:     "No release group",
			filename: "Series.Name.S01E01.mkv",
			want:     "",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tokenizer.ExtractReleaseGroup(tt.filename)
			if got != tt.want {
				t.Errorf("ExtractReleaseGroup() = %v, want %v", got, tt.want)
			}
		})
	}
}

// TestCapitalizeWord tests word capitalization rules
func TestCapitalizeWord(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name string
		word string
		want string
	}{
		{
			name: "Preserve NCIS",
			word: "NCIS",
			want: "NCIS",
		},
		{
			name: "Title case normal word",
			word: "walking",
			want: "Walking",
		},
		{
			name: "Italian article 'di'",
			word: "di",
			want: "di",
		},
		{
			name: "Italian article 'della'",
			word: "della",
			want: "della",
		},
		{
			name: "Single letter",
			word: "l",
			want: "L",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tokenizer.capitalizeWord(tt.word)
			if got != tt.want {
				t.Errorf("capitalizeWord() = %v, want %v", got, tt.want)
			}
		})
	}
}

// TestEmptyInput tests handling of empty input
func TestEmptyInput(t *testing.T) {
	tokenizer := NewItalianTokenizer()

	tests := []struct {
		name  string
		input string
	}{
		{
			name:  "Empty string",
			input: "",
		},
		{
			name:  "Whitespace only",
			input: "   ",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			seriesName := tokenizer.ExtractSeriesName(tt.input)
			if seriesName != "" {
				t.Errorf("ExtractSeriesName() = %v, want empty string", seriesName)
			}

			tokens := tokenizer.Tokenize(tt.input)
			if len(tokens) != 0 {
				t.Errorf("Tokenize() = %v, want empty slice", tokens)
			}
		})
	}
}

// BenchmarkExtractSeriesName benchmarks series name extraction
func BenchmarkExtractSeriesName(b *testing.B) {
	tokenizer := NewItalianTokenizer()
	filename := "Il Trono Di Spade 8x04 L Ultimo Degli Stark ITA WEBMux x264-UBi mkv.mkv"

	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		_ = tokenizer.ExtractSeriesName(filename)
	}
}

// BenchmarkTokenize benchmarks tokenization
func BenchmarkTokenize(b *testing.B) {
	tokenizer := NewItalianTokenizer()
	filename := "Il Trono Di Spade 8x04 L Ultimo Degli Stark ITA WEBMux x264-UBi mkv.mkv"

	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		_ = tokenizer.Tokenize(filename)
	}
}
