namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Fact-style test host. Runs all descriptors sequentially through the
    /// Touchstone executor, honoring beforeSuiteAsync/afterSuiteAsync lifecycle
    /// hooks and preserving test order.
    /// </summary>
    public sealed class RecallDbTests : TouchstoneFactBase
    {
        protected override IReadOnlyList<TestSuiteDescriptor> Suites => RecallDbSuites.All;

        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
