using CsvHelper;
using Flurl;
using Flurl.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using System.Globalization;

namespace YahooFinanceApi
{
    public sealed partial class Yahoo
    {
        public static CultureInfo Culture = CultureInfo.InvariantCulture;
        public static bool IgnoreEmptyRows { set { RowExtension.IgnoreEmptyRows = value; } }

        public static async Task<IReadOnlyList<Candle>> GetHistoricalAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, Period period = Period.Daily, CancellationToken token = default)
		    => await GetTicksAsync(symbol, 
	                               startTime, 
	                               endTime, 
	                               period, 
	                               ShowOption.History,
                                   RowExtension.ToCandle,
                                   token).ConfigureAwait(false);

        public static async Task<IReadOnlyList<DividendTick>> GetDividendsAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, CancellationToken token = default)
            => await GetTicksAsync(symbol, 
                                   startTime, 
                                   endTime, 
                                   Period.Daily, 
                                   ShowOption.Dividend,
                                   RowExtension.ToDividendTick,
                                   token).ConfigureAwait(false);

        public static async Task<IReadOnlyList<SplitTick>> GetSplitsAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, CancellationToken token = default)
            => await GetTicksAsync(symbol,
                                   startTime,
                                   endTime,
                                   Period.Daily,
                                   ShowOption.Split,
                                   RowExtension.ToSplitTick,
                                   token).ConfigureAwait(false);

        private static async Task<List<ITick>> GetTicksAsync<ITick>(
            string symbol,
            DateTime? startTime,
            DateTime? endTime,
            Period period,
            ShowOption showOption,
            Func<string[], ITick> instanceFunction,
            CancellationToken token
            )
        {
            using (var stream = await GetResponseStreamAsync(symbol, startTime, endTime, period, showOption.Name(), token).ConfigureAwait(false))
			using (var sr = new StreamReader(stream))
			using (var csvReader = new CsvReader(sr,Culture))
			{
                csvReader.Read(); // skip header

                var ticks = new List<ITick>();

                while (csvReader.Read())
                {
                    var tick = instanceFunction(csvReader.Context.Record);
#pragma warning disable RECS0017 // Possible compare of value type with 'null'
                    if (tick != null)
#pragma warning restore RECS0017 // Possible compare of value type with 'null'
                        ticks.Add(tick);
                }

                return ticks;
            }
		}

        private static async Task<Stream> GetResponseStreamAsync(string symbol, DateTime? startTime, DateTime? endTime, Period period, string events, CancellationToken token)
        {
            bool reset = false;
            while (true)
            {
                try
                {
                    var (client, crumb) = await YahooClientFactory.GetClientAndCrumbAsync(reset, token).ConfigureAwait(false);
                    return await _GetResponseStreamAsync(client, crumb, token).ConfigureAwait(false);
                }
                catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception($"Invalid ticker or endpoint for symbol '{symbol}'.", ex);
                }
                catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine("GetResponseStreamAsync: Unauthorized.");

                    if (reset)
                        throw;
                    reset = true; // try again with a new client
                }
            }

            #region Local Functions

            Task<Stream> _GetResponseStreamAsync(IFlurlClient _client, string _crumb, CancellationToken _token)
            {
                // Yahoo expects dates to be "Eastern Standard Time"
                startTime = startTime?.FromEstToUtc() ?? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                endTime =   endTime?  .FromEstToUtc() ?? DateTime.UtcNow;

                var url = "https://query1.finance.yahoo.com/v7/finance/download"
                    .AppendPathSegment(symbol)
                    .SetQueryParam("period1", startTime.Value.ToUnixTimestamp())
                    .SetQueryParam("period2", endTime.Value.ToUnixTimestamp())
                    .SetQueryParam("interval", $"1{period.Name()}")
                    .SetQueryParam("events", events)
                    .SetQueryParam("crumb", _crumb);

                Debug.WriteLine(url);

                return url
                    .WithClient(_client)
                    .GetAsync(_token)
                    .ReceiveStream();
            }

            #endregion
        }
    }
}
