using StockNewsNotifier.Tests.Crawlers;

var tests = new (string Name, Func<(bool Ok, string? Error)> Run)[]
{
    ("YahooFinanceHtmlParser fixture", () =>
    {
        var ok = YahooFinanceHtmlFixtureSmokeTest.Run(out var error);
        return (ok, error);
    }),
    ("YahooFinanceCrawler HTTP fixture", () =>
    {
        var ok = YahooFinanceCrawlerHttpSmokeTest.Run(out var error);
        return (ok, error);
    })
};

var failures = 0;

foreach (var test in tests)
{
    var (ok, error) = test.Run();
    if (ok)
    {
        Console.WriteLine($"{test.Name}: PASS");
    }
    else
    {
        failures++;
        Console.Error.WriteLine($"{test.Name}: FAIL");
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.Error.WriteLine(error);
        }
    }
}

Environment.ExitCode = failures == 0 ? 0 : 1;
