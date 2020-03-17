using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace YahooFinanceApi.Tests
{
    public class Historical
    {
        private readonly Action<string> Write;
        public Historical(ITestOutputHelper output) => Write = output.WriteLine;

        [Fact]
        public async Task InvalidSymbolTest()
        {
            var exception = await Assert.ThrowsAsync<Exception>(async () =>
                await Yahoo.GetHistoricalAsync("invalidSymbol", new DateTime(2017, 1, 3), new DateTime(2017, 1, 4)));

            Write(exception.ToString());

            Assert.Contains("Not Found", exception.InnerException.Message);
        }

        [Fact]
        public async Task PeriodTest()
        {
            var candles = await Yahoo.GetHistoricalAsync("AAPL", new DateTime(2017, 1, 3), DateTime.Now, Period.Daily);
            Assert.Equal(115.800003m, candles.First().Open);

            candles = await Yahoo.GetHistoricalAsync("AAPL", new DateTime(2017, 1, 3), DateTime.Now, Period.Weekly);
            Assert.Equal(115.800003m, candles.First().Open);

            candles = await Yahoo.GetHistoricalAsync("AAPL", new DateTime(2017, 1, 3), DateTime.Now, Period.Monthly);
            Assert.Equal(115.800003m, candles.First().Open);
        }

        [Fact]
        public async Task HistoricalTest()
        {
            var candles = await Yahoo.GetHistoricalAsync("AAPL", new DateTime(2017, 1, 3), new DateTime(2017, 1, 4), Period.Daily);

            Assert.Equal(115.800003m, candles.First().Open);
            Assert.Equal(116.330002m, candles.First().High);
            Assert.Equal(114.760002m, candles.First().Low);
            Assert.Equal(116.150002m, candles.First().Close);
            Assert.Equal(28_781_900, candles.First().Volume);
        }

        [Fact]
        public async Task DividendTest()
        {
            var dividends = await Yahoo.GetDividendsAsync("AAPL", new DateTime(2016, 2, 4), new DateTime(2016, 2, 5));
            Assert.Equal(0.52m, dividends.First().Dividend);
        }

        [Fact]
        public async Task SplitTest()
        {
            var splits = await Yahoo.GetSplitsAsync("AAPL", new DateTime(2011, 6, 8), new DateTime(2017, 6, 10));

            Assert.Equal(7, splits.First().BeforeSplit);
            Assert.Equal(1, splits.First().AfterSplit);
        }

        [Fact]
        public async Task DatesTest_US()
        {
            var from = new DateTime(2017, 10, 10);
            var to   = new DateTime(2017, 10, 12);

            var candles = await Yahoo.GetHistoricalAsync("C", from, to, Period.Daily);

            Assert.Equal(3, candles.Count());

            Assert.Equal(from, candles.First().DateTime);
            Assert.Equal(to,   candles.Last().DateTime);

            Assert.Equal(75.18m,     candles[0].Close);
            Assert.Equal(74.940002m, candles[1].Close);
            Assert.Equal(72.370003m, candles[2].Close);
        }

        [Fact]
        public async Task Test_UK()
        {
            var from = new DateTime(2017, 10, 10);
            var to = new DateTime(2017, 10, 13);

            var candles = await Yahoo.GetHistoricalAsync("BA.L", from, to, Period.Daily);

            Assert.Equal(3, candles.Count());

            Assert.Equal(from, candles.First().DateTime);
            Assert.Equal(to,   candles.Last().DateTime.AddDays(1));

            Assert.Equal(616.50m, candles[0].Close);
            Assert.Equal(615.00m, candles[1].Close);
            Assert.Equal(616.00m, candles[2].Close);
        }

        [Fact]
        public async Task DatesTest_TW()
        {
            var from = new DateTime(2017, 10, 11);
            var to = new DateTime(2017, 10, 13);

            var candles = await Yahoo.GetHistoricalAsync("2498.TW", from, to, Period.Daily);

            Assert.Equal(3, candles.Count());

            Assert.Equal(from, candles.First().DateTime);
            Assert.Equal(to,   candles.Last().DateTime);

            Assert.Equal(71.599998m, candles[0].Close);
            Assert.Equal(71.599998m, candles[1].Close);
            Assert.Equal(73.099998m, candles[2].Close);
        }

        [Theory]
        [InlineData("SPY")] // USA
        [InlineData("TD.TO")] // Canada
        [InlineData("BP.L")] // London
        [InlineData("AIR.PA")] // Euronext
        [InlineData("AIR.DE")] // Xetra
        [InlineData("UNITECH.BO")] // Bombay
        [InlineData("2800.HK")] // Hong Kong
        [InlineData("000001.SS")] // Shanghai
        [InlineData("2448.TW")] // Taiwan
        [InlineData("005930.KS")] // Korea
        [InlineData("BHP.AX")] // Sydney
        public async Task DatesTest(params string[] symbols)
        {
            var from = new DateTime(2017, 9, 12);
            var to = from.AddDays(2);

            // start tasks
            var tasks = symbols.Select(symbol => Yahoo.GetHistoricalAsync(symbol, from, to));

            // wait for all tasks to complete
            var results = await Task.WhenAll(tasks.ToArray());

            foreach (var candles in results)
            {
                Assert.Equal(3, candles.Count());

                Assert.Equal(from, candles.First().DateTime);
                Assert.Equal(to,   candles.Last().DateTime);
            }
        }

        [Fact]
        public async Task TestLatest()
        {
            var candles = await Yahoo.GetHistoricalAsync("C", DateTime.Now.AddDays(-7));
            foreach (var candle in candles)
                Write($"{candle.DateTime} {candle.Close}");
        }

        [Fact]
        public async Task CurrencyTest()
        {
            var from = new DateTime(2017, 10, 10);
            var to = new DateTime(2017, 10, 12);

            var candles = await Yahoo.GetHistoricalAsync("EURUSD=X", from, to);

            foreach (var candle in candles)
                Write($"{candle.DateTime} {candle.Close}");

            Assert.Equal(3, candles.Count());

            Assert.Equal(1.174164m, candles[0].Close);
            Assert.Equal(1.181488m, candles[1].Close);
            Assert.Equal(1.186549m, candles[2].Close);

            // Note: Forex seems to return date = (requested date - 1 day)
            Assert.Equal(from, candles.First().DateTime);
            Assert.Equal(to, candles.Last().DateTime);
        }
    }
}
