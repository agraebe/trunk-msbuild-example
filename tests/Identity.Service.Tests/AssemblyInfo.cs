using Xunit;

// Explicitly force cross-collection parallelization for this test assembly. The
// FlakyTests.cs shared-state race depends on FirstTenantOnboardingFlakyTests and
// SecondTenantOnboardingFlakyTests (two separate implicit collections) actually
// running concurrently — some hosts default this off, which would make the
// "flaky" test deterministic instead of racy.
[assembly: CollectionBehavior(DisableTestParallelization = false, MaxParallelThreads = 4)]
