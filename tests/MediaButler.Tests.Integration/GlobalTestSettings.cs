using Xunit;

// Disable parallelization for integration tests to avoid SQLite locking issues
// during high-concurrency test scenarios.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
