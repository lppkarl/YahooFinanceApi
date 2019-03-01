﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using NodaTime;
using NodaTime.TimeZones;
using System.Threading;
using Flurl.Http;

namespace YahooFinanceApi.Tests
{
    public class YahooHistoryTests
    {
        private readonly Action<string> Write;
        public YahooHistoryTests(ITestOutputHelper output) => Write = output.WriteLine;

        [Fact]
        public async Task SimpleTest()
        {
            List<HistoryTick> ticks = await new YahooHistory()
                .Period(Duration.FromDays(10))
                .GetHistoryAsync("C");

            Assert.NotEmpty(ticks);
            Assert.True(ticks[0].Close > 0);

            Assert.Null(await new YahooHistory().GetHistoryAsync("badSymbol"));
        }

        [Fact]
        public async Task TestSymbols()
        {
            string[] symbols = new [] { "C", "badSymbol" };
            Dictionary<string, List<HistoryTick>> dictionary = await new YahooHistory().GetHistoryAsync(symbols);
            Assert.Equal(symbols.Length, dictionary.Count);

            List<HistoryTick> ticks = dictionary["C"];
            Assert.True(ticks[0].Close > 0);

            Assert.Null(dictionary["badSymbol"]);
        }

        [Fact]
        public void TestSymbolsArgument()
        {
            var y = new YahooHistory();
            Assert.ThrowsAsync<ArgumentException>(async () => await y.GetHistoryAsync(""));
            Assert.ThrowsAsync<ArgumentException>(async () => await y.GetHistoryAsync(new string[] { }));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await y.GetHistoryAsync(new string[] { null }));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await y.GetHistoryAsync(new string[] { "C", null }));
            Assert.ThrowsAsync<ArgumentException>(async () => await y.GetHistoryAsync(new string[] { "" }));
            Assert.ThrowsAsync<ArgumentException>(async () => await y.GetHistoryAsync(new string[] { "C", "" }));
        }

        [Fact]
        public async Task TestDuplicateSymbols()
        {
            var y = new YahooHistory();
            var exception = await Assert.ThrowsAsync<ArgumentException>
                (async () => await y.GetHistoryAsync(new[] { "C", "X", "C" }));
            Assert.StartsWith("Duplicate symbol(s): \"C\".", exception.Message);
        }

        [Fact]
        public async Task TestPeriodWithDuration() // Duration does not take into account calendar or timezone.
        {
            // default frequency is daily
            var ticks = await new YahooHistory().Period(Duration.FromDays(10)).GetHistoryAsync("C");
            foreach (var tick in ticks)
                Write($"{tick.Date} {tick.Close}");
            Assert.True(ticks.Count > 3);
        }

        [Fact]
        public async Task TestPeriodWithUnixTimeSeconds()
        {
            LocalDateTime dt = new LocalDateTime(2019, 1, 7, 16, 0);
            ZonedDateTime zdt = dt.InZoneLeniently("America/New_York".ToDateTimeZone());
            long seconds = zdt.ToInstant().ToUnixTimeSeconds();

            var ticks = await new YahooHistory().Period(seconds).GetHistoryAsync("C");
            foreach (var tick in ticks)
                Write($"{tick.Date} {tick.Close}");
            Assert.Equal(ticks[0].Date, dt.Date);
        }

        [Fact]
        public async Task TestPeriodWithDate()
        {
            DateTimeZone dateTimeZone = "Asia/Taipei".ToDateTimeZone();
            LocalDate localDate = new LocalDate(2019, 1, 7);

            var ticks = await new YahooHistory().Period(dateTimeZone, localDate).GetHistoryAsync("2448.TW");
            foreach (var tick in ticks)
                Write($"{tick.Date} {tick.Close}");
            Assert.Equal(ticks[0].Date, localDate);
        }

        [Fact]
        public async Task TestHistoryTickTest()
        {
            DateTimeZone dateTimeZone = "America/New_York".ToDateTimeZone();
            LocalDate localDate1 = new LocalDate(2017, 1, 3);
            LocalDate localDate2 = new LocalDate(2017, 1, 4);

            var ticks = await new YahooHistory()
                .Period(dateTimeZone, localDate1, localDate2)
                .GetHistoryAsync("AAPL", Frequency.Daily);

            Assert.Equal(2, ticks.Count());

            var tick = ticks[0];
            Assert.Equal(115.800003m, tick.Open);
            Assert.Equal(116.330002m, tick.High);
            Assert.Equal(114.760002m, tick.Low);
            Assert.Equal(116.150002m, tick.Close);
            Assert.Equal(28_781_900, tick.Volume);

            foreach (var t in ticks)
                Write($"{t.Date} {t.Close}");
        }

        [Fact]
        public async Task TestDividend()
        {
            DateTimeZone dateTimeZone = "America/New_York".ToDateTimeZone();
            var dividends = await new YahooHistory()
                .Period(dateTimeZone, new LocalDate(2016, 2, 4), new LocalDate(2016, 2, 5))
                .GetDividendsAsync("AAPL");
            Assert.Equal(0.52m, dividends[0].Dividend);
        }

        [Fact]
        public async Task TestSplit()
        {
            DateTimeZone dateTimeZone = "America/New_York".ToDateTimeZone();
            var splits = await new YahooHistory()
                .Period(dateTimeZone, new LocalDate(2014, 6, 8), new LocalDate(2014, 6, 10))
                .GetSplitsAsync("AAPL");
            Assert.Equal(7, splits[0].BeforeSplit);
            Assert.Equal(1, splits[0].AfterSplit);
        }

        [Fact]
        public async Task TestDates_US()
        {
            DateTimeZone dateTimeZone = "America/New_York".ToDateTimeZone();
            var from = new LocalDate(2017, 10, 10);
            var to = new LocalDate(2017, 10, 12);

            var ticks = await new YahooHistory().Period(dateTimeZone, from, to)
                .GetHistoryAsync("C", Frequency.Daily);

            Assert.Equal(from, ticks.First().Date);
            Assert.Equal(to, ticks.Last().Date);

            Assert.Equal(3, ticks.Count());
            Assert.Equal(75.18m, ticks[0].Close);
            Assert.Equal(74.940002m, ticks[1].Close);
            Assert.Equal(72.370003m, ticks[2].Close);
        }

        [Fact]
        public async Task TestDates_UK()
        {
            DateTimeZone dateTimeZone = "Europe/London".ToDateTimeZone();

            var from = new LocalDate(2017, 10, 10);
            var to = new LocalDate(2017, 10, 12);

            var ticks = await new YahooHistory().Period(dateTimeZone, from, to)
                .GetHistoryAsync("BA.L", Frequency.Daily);

            Assert.Equal(from, ticks.First().Date);
            Assert.Equal(to, ticks.Last().Date);

            Assert.Equal(3, ticks.Count());
            Assert.Equal(616.50m, ticks[0].Close);
            Assert.Equal(615.00m, ticks[1].Close);
            Assert.Equal(616.00m, ticks[2].Close);
        }

        [Fact]
        public async Task TestDates_TW()
        {
            DateTimeZone dateTimeZone = "Asia/Taipei".ToDateTimeZone();

            var from = new LocalDate(2017, 10, 11);
            var to = new LocalDate(2017, 10, 13);

            var ticks = await new YahooHistory().Period(dateTimeZone, from, to)
                .GetHistoryAsync("2498.TW", Frequency.Daily);

            Assert.Equal(from, ticks.First().Date);
            Assert.Equal(to, ticks.Last().Date);

            Assert.Equal(3, ticks.Count());
            Assert.Equal(71.599998m, ticks[0].Close);
            Assert.Equal(71.599998m, ticks[1].Close);
            Assert.Equal(73.099998m, ticks[2].Close);
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
        public async Task TestDates(string symbol)
        {
            var security = await new YahooQuotes().GetAsync(symbol);
            var timeZoneName = security.ExchangeTimezoneName;
            var timeZone = timeZoneName.ToDateTimeZone();

            var from = new LocalDate(2017, 9, 12);
            var to = from.PlusDays(2);

            var ticks = await new YahooHistory().Period(timeZone, from, to)
                .GetHistoryAsync(symbol);

            Assert.Equal(from, ticks.First().Date);
            Assert.Equal(to, ticks.Last().Date);
            Assert.Equal(3, ticks.Count());
        }

        [Fact]
        public async Task TestCurrency()
        {
            var symbol = "EURUSD=X";
            var security = await new YahooQuotes().GetAsync(symbol);
            var timeZone = security.ExchangeTimezoneName.ToDateTimeZone();

            // Note: Forex seems to return date = (requested date - 1 day)
            var from = new LocalDate(2017, 10, 10);
            var to = from.PlusDays(2);

            var ticks = await new YahooHistory().Period(timeZone, from, to)
                .GetHistoryAsync("EURUSD=X");

            foreach (var tick in ticks)
                Write($"{tick.Date} {tick.Close}");

            Assert.Equal(from, ticks.First().Date.PlusDays(1));
            Assert.Equal(to, ticks.Last().Date.PlusDays(1));

            Assert.Equal(3, ticks.Count());
            Assert.Equal(1.174164m, ticks[0].Close);
            Assert.Equal(1.181488m, ticks[1].Close);
            Assert.Equal(1.186549m, ticks[2].Close);
        }

        [Fact]
        public async Task TestFrequency()
        {
            var symbol = "AAPL";
            var timeZone = "America/New_York".ToDateTimeZone();
            var startDate = new LocalDate(2019, 1, 10);

            var ticks1 = await new YahooHistory().Period(timeZone, startDate).GetHistoryAsync(symbol, Frequency.Daily);
            Assert.Equal(new LocalDate(2019, 1, 10), ticks1[0].Date);
            Assert.Equal(new LocalDate(2019, 1, 11), ticks1[1].Date);
            Assert.Equal(152.880005m, ticks1[1].Open);

            var ticks2 = await new YahooHistory().Period(timeZone, startDate).GetHistoryAsync(symbol, Frequency.Weekly);
            Assert.Equal(new LocalDate(2019, 1, 7), ticks2[0].Date); // previous Monday
            Assert.Equal(new LocalDate(2019, 1, 14), ticks2[1].Date);
            Assert.Equal(150.850006m, ticks2[1].Open);

            var ticks3 = await new YahooHistory().Period(timeZone, startDate).GetHistoryAsync(symbol, Frequency.Monthly);
            Assert.Equal(new LocalDate(2019, 1, 1), ticks3[0].Date); // previous start of month
            Assert.Equal(new LocalDate(2019, 2, 1), ticks3[1].Date);
            Assert.Equal(166.960007m, ticks3[1].Open);
        }

        private List<string> GetSymbols(int number)
        {
            return File.ReadAllLines(@"..\..\..\symbols.txt")
                .Where(line => !line.StartsWith("#"))
                .Take(number)
                .ToList();
        }

        [Fact]
        public async Task TestManySymbols()
        {
            var symbols = GetSymbols(10);

            var results = await new YahooHistory().Period(Duration.FromDays(10)).GetHistoryAsync(symbols);
            var invalidSymbols = results.Where(r => r.Value == null).Count();

            // If (message.StartsWith("Call failed. Collection was modified"))
            // this is a bug in Flurl: https://github.com/tmenier/Flurl/issues/398

            Write("");
            Write($"Total Symbols:   {symbols.Count}");
            Write($"Invalid Symbols: {invalidSymbols}");
        }

        [Fact]
        public async Task TestCancellationTimeout()
        {
            var cts = new CancellationTokenSource();
            //cts.CancelAfter(20);

            var task = new YahooHistory(cts.Token).Period(Duration.FromDays(10)).GetHistoryAsync(GetSymbols(5));

            cts.Cancel();

            await Assert.ThrowsAnyAsync<Exception>(async () => await task);
        }

    }
}