using StockNewsNotifier.Tests.Crawlers;

var ok = YahooFinanceHtmlFixtureSmokeTest.Run(out var error);
if (ok)
{
    Console.WriteLine("YahooFinanceHtmlParser fixture smoke test passed.");
}
else
{
    Console.Error.WriteLine("YahooFinanceHtmlParser fixture smoke test failed:");
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
