using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Nop.Core;


namespace Nop.Plugin.Payments.Paytm.Services
{
    public partial class PaytmHttpClient
    {
        #region Fields

        private readonly HttpClient _httpClient;
        private readonly PaytmPaymentSettings _paytmPaymentSettings;

        #endregion

        #region Ctor

        public PaytmHttpClient(HttpClient client,
            PaytmPaymentSettings paytmPaymentSettings)
        {
            //configure client
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CurrentVersion}");

            _httpClient = client;
            _paytmPaymentSettings = paytmPaymentSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <returns>The asynchronous task whose result contains the PDT details</returns>
        public async Task<string> GetPdtDetailsAsync(string tx)
        {
            //get response
            var url = _paytmPaymentSettings.UseDefaultCallBack ?
          "https://www.sandbox.paytm.com/us/cgi-bin/webscr" :
              "https://www.paytm.com/us/cgi-bin/webscr";
            var requestContent = new StringContent($"cmd=_notify-synch&at={_paytmPaymentSettings.PdtToken}&tx={tx}",
                Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);
            var response = await _httpClient.PostAsync(url, requestContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <returns>The asynchronous task whose result contains the IPN verification details</returns>
        public async Task<string> VerifyIpnAsync(string formString)
        {
            //get response
            var url = _paytmPaymentSettings.UseDefaultCallBack ?
                 "https://ipnpb.sandbox.paytm.com/cgi-bin/webscr" :
                 "https://ipnpb.paytm.com/cgi-bin/webscr";
            var requestContent = new StringContent($"cmd=_notify-validate&{formString}",
                Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);
            var response = await _httpClient.PostAsync(url, requestContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        #endregion
    }
}
