package tfidf

import (
	"math"
	"testing"
)

const epsilon = 1e-6 // Tolerance for floating point comparison

// floatEquals checks if two floats are equal within epsilon
func floatEquals(a, b float64) bool {
	return math.Abs(a-b) < epsilon
}

// TestComputeTF tests term frequency calculation
func TestComputeTF(t *testing.T) {
	tests := []struct {
		name   string
		tokens []string
		want   map[string]float64
	}{
		{
			name:   "Simple tokens",
			tokens: []string{"the", "cat", "sat", "on", "the", "mat"},
			want: map[string]float64{
				"the": 2.0 / 6.0, // 0.333...
				"cat": 1.0 / 6.0, // 0.166...
				"sat": 1.0 / 6.0,
				"on":  1.0 / 6.0,
				"mat": 1.0 / 6.0,
			},
		},
		{
			name:   "Single token",
			tokens: []string{"hello"},
			want: map[string]float64{
				"hello": 1.0,
			},
		},
		{
			name:   "All same tokens",
			tokens: []string{"foo", "foo", "foo"},
			want: map[string]float64{
				"foo": 1.0,
			},
		},
		{
			name:   "Empty tokens",
			tokens: []string{},
			want:   map[string]float64{},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := ComputeTF(tt.tokens)

			if len(got) != len(tt.want) {
				t.Errorf("ComputeTF() got %d tokens, want %d", len(got), len(tt.want))
			}

			for token, wantValue := range tt.want {
				gotValue, exists := got[token]
				if !exists {
					t.Errorf("ComputeTF() missing token %q", token)
					continue
				}

				if !floatEquals(gotValue, wantValue) {
					t.Errorf("ComputeTF() token %q = %v, want %v", token, gotValue, wantValue)
				}
			}
		})
	}
}

// TestFitFromDocuments tests IDF computation from documents
func TestFitFromDocuments(t *testing.T) {
	// Test documents (sklearn-compatible example)
	documents := [][]string{
		{"the", "cat", "sat"},
		{"the", "dog", "sat"},
		{"the", "bird", "flew"},
	}

	vectorizer := NewTFIDFVectorizer()
	err := vectorizer.FitFromDocuments(documents)
	if err != nil {
		t.Fatalf("FitFromDocuments() error = %v", err)
	}

	// Verify number of documents
	if vectorizer.GetNumDocs() != 3 {
		t.Errorf("GetNumDocs() = %v, want 3", vectorizer.GetNumDocs())
	}

	// Verify vocabulary size
	if vectorizer.GetVocabularySize() != 6 {
		t.Errorf("GetVocabularySize() = %v, want 6", vectorizer.GetVocabularySize())
	}

	// Test specific IDF values
	// Formula: IDF = log((1 + num_docs) / (1 + doc_freq)) + 1
	tests := []struct {
		token   string
		docFreq int
		wantIDF float64
	}{
		{
			token:   "the",
			docFreq: 3, // Appears in all documents
			wantIDF: math.Log(float64(1+3)/float64(1+3)) + 1.0, // = 1.0
		},
		{
			token:   "cat",
			docFreq: 1, // Appears in 1 document
			wantIDF: math.Log(float64(1+3)/float64(1+1)) + 1.0, // ≈ 1.693
		},
		{
			token:   "sat",
			docFreq: 2, // Appears in 2 documents
			wantIDF: math.Log(float64(1+3)/float64(1+2)) + 1.0, // ≈ 1.288
		},
	}

	for _, tt := range tests {
		t.Run("IDF_"+tt.token, func(t *testing.T) {
			gotIDF, exists := vectorizer.GetIDF(tt.token)
			if !exists {
				t.Errorf("GetIDF(%q) not found in vocabulary", tt.token)
				return
			}

			if !floatEquals(gotIDF, tt.wantIDF) {
				t.Errorf("GetIDF(%q) = %v, want %v", tt.token, gotIDF, tt.wantIDF)
			}
		})
	}
}

// TestTransform tests TF-IDF transformation
func TestTransform(t *testing.T) {
	// Train on simple documents
	documents := [][]string{
		{"cat", "sat", "mat"},
		{"dog", "sat", "mat"},
		{"bird", "flew", "away"},
	}

	vectorizer := NewTFIDFVectorizer()
	err := vectorizer.FitFromDocuments(documents)
	if err != nil {
		t.Fatalf("FitFromDocuments() error = %v", err)
	}

	// Transform a new document
	tokens := []string{"cat", "sat", "mat"}
	tfidf := vectorizer.Transform(tokens)

	// Verify that all tokens have TF-IDF scores
	if len(tfidf) != 3 {
		t.Errorf("Transform() got %d tokens, want 3", len(tfidf))
	}

	// Verify that TF-IDF values are positive
	for token, value := range tfidf {
		if value <= 0 {
			t.Errorf("Transform() token %q has non-positive TF-IDF = %v", token, value)
		}
	}

	// "cat" should have higher TF-IDF than "sat" (appears in fewer documents)
	catTFIDF := tfidf["cat"]
	satTFIDF := tfidf["sat"]

	if catTFIDF <= satTFIDF {
		t.Errorf("Transform() cat TF-IDF (%v) should be > sat TF-IDF (%v)", catTFIDF, satTFIDF)
	}
}

// TestTransform_UnknownTokens tests handling of tokens not in vocabulary
func TestTransform_UnknownTokens(t *testing.T) {
	// Train on simple documents
	documents := [][]string{
		{"cat", "dog"},
		{"bird", "fish"},
	}

	vectorizer := NewTFIDFVectorizer()
	err := vectorizer.FitFromDocuments(documents)
	if err != nil {
		t.Fatalf("FitFromDocuments() error = %v", err)
	}

	// Transform with unknown tokens
	tokens := []string{"elephant", "zebra"} // Not in training vocabulary
	tfidf := vectorizer.Transform(tokens)

	// Should still produce TF-IDF scores (with smoothed IDF)
	if len(tfidf) != 2 {
		t.Errorf("Transform() got %d tokens, want 2", len(tfidf))
	}

	// Unknown tokens should have positive TF-IDF
	for token, value := range tfidf {
		if value <= 0 {
			t.Errorf("Transform() unknown token %q has non-positive TF-IDF = %v", token, value)
		}
	}
}

// TestFitTransform tests the combined fit and transform operation
func TestFitTransform(t *testing.T) {
	documents := [][]string{
		{"breaking", "bad"},
		{"the", "walking", "dead"},
		{"game", "of", "thrones"},
	}

	vectorizer := NewTFIDFVectorizer()
	tfidfVectors, err := vectorizer.FitTransform(documents)
	if err != nil {
		t.Fatalf("FitTransform() error = %v", err)
	}

	// Should have 3 TF-IDF vectors (one per document)
	if len(tfidfVectors) != 3 {
		t.Errorf("FitTransform() got %d vectors, want 3", len(tfidfVectors))
	}

	// Each vector should have TF-IDF scores
	for i, vec := range tfidfVectors {
		if len(vec) == 0 {
			t.Errorf("FitTransform() vector %d is empty", i)
		}

		for token, value := range vec {
			if value <= 0 {
				t.Errorf("FitTransform() vector %d, token %q has non-positive value = %v", i, token, value)
			}
		}
	}
}

// TestL2Normalize tests L2 normalization
func TestL2Normalize(t *testing.T) {
	tests := []struct {
		name  string
		input map[string]float64
		want  float64 // Expected L2 norm after normalization (should be 1.0)
	}{
		{
			name: "Simple vector",
			input: map[string]float64{
				"a": 3.0,
				"b": 4.0,
			},
			want: 1.0, // L2 norm should be 1.0 after normalization
		},
		{
			name: "Unit vector",
			input: map[string]float64{
				"a": 1.0,
			},
			want: 1.0,
		},
		{
			name: "All same values",
			input: map[string]float64{
				"a": 1.0,
				"b": 1.0,
				"c": 1.0,
			},
			want: 1.0,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			normalized := L2Normalize(tt.input)

			// Compute L2 norm of normalized vector
			var sumSquares float64
			for _, value := range normalized {
				sumSquares += value * value
			}
			norm := math.Sqrt(sumSquares)

			if !floatEquals(norm, tt.want) {
				t.Errorf("L2Normalize() norm = %v, want %v", norm, tt.want)
			}
		})
	}
}

// TestL2Normalize_EmptyVector tests L2 normalization of empty vector
func TestL2Normalize_EmptyVector(t *testing.T) {
	input := map[string]float64{}
	normalized := L2Normalize(input)

	if len(normalized) != 0 {
		t.Errorf("L2Normalize() empty vector = %v, want empty", normalized)
	}
}

// TestCosineSimilarity tests cosine similarity calculation
func TestCosineSimilarity(t *testing.T) {
	tests := []struct {
		name string
		vec1 map[string]float64
		vec2 map[string]float64
		want float64
	}{
		{
			name: "Identical vectors",
			vec1: map[string]float64{"a": 1.0, "b": 1.0, "c": 1.0},
			vec2: map[string]float64{"a": 1.0, "b": 1.0, "c": 1.0},
			want: 1.0, // Perfect similarity
		},
		{
			name: "Orthogonal vectors",
			vec1: map[string]float64{"a": 1.0, "b": 0.0},
			vec2: map[string]float64{"a": 0.0, "b": 1.0},
			want: 0.0, // No similarity
		},
		{
			name: "Partial overlap",
			vec1: map[string]float64{"a": 1.0, "b": 1.0},
			vec2: map[string]float64{"a": 1.0, "c": 1.0},
			want: 0.5, // 50% similarity (one overlapping dimension)
		},
		{
			name: "Opposite vectors",
			vec1: map[string]float64{"a": 1.0},
			vec2: map[string]float64{"a": -1.0},
			want: -1.0, // Perfect dissimilarity
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := CosineSimilarity(tt.vec1, tt.vec2)
			if !floatEquals(got, tt.want) {
				t.Errorf("CosineSimilarity() = %v, want %v", got, tt.want)
			}
		})
	}
}

// TestCosineSimilarity_EmptyVectors tests cosine similarity with empty vectors
func TestCosineSimilarity_EmptyVectors(t *testing.T) {
	vec1 := map[string]float64{}
	vec2 := map[string]float64{"a": 1.0}

	similarity := CosineSimilarity(vec1, vec2)
	if similarity != 0.0 {
		t.Errorf("CosineSimilarity() with empty vector = %v, want 0.0", similarity)
	}
}

// TestRealWorldExample tests TF-IDF on TV series filenames
func TestRealWorldExample(t *testing.T) {
	// Real-world documents (TV series names as tokens)
	documents := [][]string{
		{"breaking", "bad"},
		{"breaking", "bad"},
		{"the", "walking", "dead"},
		{"the", "walking", "dead"},
		{"game", "of", "thrones"},
	}

	vectorizer := NewTFIDFVectorizer()
	err := vectorizer.FitFromDocuments(documents)
	if err != nil {
		t.Fatalf("FitFromDocuments() error = %v", err)
	}

	// Transform "breaking bad"
	breakingBad := vectorizer.Transform([]string{"breaking", "bad"})

	// Transform "the walking dead"
	walkingDead := vectorizer.Transform([]string{"the", "walking", "dead"})

	// Verify that series-specific tokens have higher weight than common tokens
	// "breaking" should be more important than "the" (appears in fewer docs)
	breakingTFIDF := breakingBad["breaking"]
	theTFIDF := walkingDead["the"]

	if breakingTFIDF <= theTFIDF {
		t.Errorf("TF-IDF: 'breaking' (%v) should be > 'the' (%v)", breakingTFIDF, theTFIDF)
	}

	t.Logf("TF-IDF scores:")
	t.Logf("  'breaking': %.4f", breakingTFIDF)
	t.Logf("  'the': %.4f", theTFIDF)
}

// BenchmarkComputeTF benchmarks TF computation
func BenchmarkComputeTF(b *testing.B) {
	tokens := []string{"the", "cat", "sat", "on", "the", "mat", "and", "the", "dog", "sat"}

	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		_ = ComputeTF(tokens)
	}
}

// BenchmarkTransform benchmarks TF-IDF transformation
func BenchmarkTransform(b *testing.B) {
	documents := [][]string{
		{"breaking", "bad"},
		{"the", "walking", "dead"},
		{"game", "of", "thrones"},
		{"one", "piece"},
		{"attack", "on", "titan"},
	}

	vectorizer := NewTFIDFVectorizer()
	_ = vectorizer.FitFromDocuments(documents)

	tokens := []string{"breaking", "bad"}

	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		_ = vectorizer.Transform(tokens)
	}
}
