using Test.Shared;
using Touchstone.Cli;

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
