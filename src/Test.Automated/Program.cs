using System;
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
//   dotnet run --project src/Test.Automated -- --help
//
// The endpoint and API key also fall back to the RECALLDB_ENDPOINT and
// RECALLDB_APIKEY environment variables (applied in Test.Shared.TestHelpers).

string resultsPath = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--help" || args[i] == "-h" || args[i] == "-?" || args[i] == "/?")
    {
        Console.WriteLine("RecallDB integration test runner (Touchstone)");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/Test.Automated [-- <options>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --endpoint <url>    REST base URL to target (default: http://127.0.0.1:8600)");
        Console.WriteLine("  --apikey <key>      Admin API key for authentication (default: recalldbadmin)");
        Console.WriteLine("  --results <path>    Write the JSON results report to <path>");
        Console.WriteLine("  -h, --help, -?, /?  Show this help and exit");
        Console.WriteLine();
        Console.WriteLine("Environment variables (used when the matching flag is not supplied):");
        Console.WriteLine("  RECALLDB_ENDPOINT       Overrides --endpoint");
        Console.WriteLine("  RECALLDB_APIKEY         Overrides --apikey");
        Console.WriteLine("  RECALLDB_MCP_ENDPOINT   MCP Streamable HTTP endpoint (default: http://127.0.0.1:8620)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project src/Test.Automated");
        Console.WriteLine("  dotnet run --project src/Test.Automated -- --endpoint http://127.0.0.1:8600 --apikey recalldbadmin");
        Console.WriteLine("  dotnet run --project src/Test.Automated -- --results results.json");
        Console.WriteLine();
        Console.WriteLine("Note: the suites create and delete real data against the target endpoint (they self-clean).");
        return 0;
    }
    else if (args[i] == "--endpoint" && i + 1 < args.Length)
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
