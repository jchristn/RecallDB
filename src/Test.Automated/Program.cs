using Test.Shared;
using Touchstone.Cli;

// Touchstone CLI runner for the RecallDB integration test suites.
// Suites are defined centrally in Test.Shared (RecallDbSuites) and are shared
// by the Touchstone xUnit (Test.Xunit) and NUnit (Test.Nunit) adapters.
//
// Usage:
//   dotnet run --project src/Test.Automated
//   dotnet run --project src/Test.Automated -- --endpoint http://127.0.0.1:8600 --apikey recalldbadmin
//   dotnet run --project src/Test.Automated -- --results results.json
//
// The endpoint and API key also fall back to the RECALLDB_ENDPOINT and
// RECALLDB_APIKEY environment variables (applied in Test.Shared.TestHelpers).

string resultsPath = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--endpoint" && i + 1 < args.Length)
    {
        TestHelpers.Endpoint = args[i + 1].TrimEnd('/');
        i++;
    }
    else if (args[i] == "--apikey" && i + 1 < args.Length)
    {
        TestHelpers.ApiKey = args[i + 1];
        i++;
    }
    else if (args[i] == "--results" && i + 1 < args.Length)
    {
        resultsPath = args[i + 1];
        i++;
    }
}

return await ConsoleRunner.RunAsync(RecallDbSuites.All, resultsPath: resultsPath);
