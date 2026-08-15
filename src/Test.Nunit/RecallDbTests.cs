namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using global::NUnit.Framework;

    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// NUnit test host. Runs all RecallDB integration test descriptors
    /// sequentially through the Touchstone executor, honoring the
    /// beforeSuiteAsync/afterSuiteAsync lifecycle hooks and preserving order.
    /// The suite definitions live in Test.Shared (the single source of truth)
    /// and are shared with the Touchstone CLI runner (Test.Automated) and the
    /// xUnit adapter (Test.Xunit).
    /// </summary>
    [TestFixture]
    public sealed class RecallDbTests : TouchstoneNunitBase
    {
        protected override IReadOnlyList<TestSuiteDescriptor> Suites => RecallDbSuites.All;

        [Test]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
