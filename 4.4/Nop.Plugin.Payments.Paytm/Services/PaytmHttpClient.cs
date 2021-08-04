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
        private readonly PaytmPaymentSettings _PaytmPaymentSettings;

        #endregion

        #region Ctor

        public PaytmHttpClient(HttpClient client,
            PaytmPaymentSettings PaytmPaymentSettings)
        {
            //configure client
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CURRENT_VERSION}");

            _httpClient = client;
            _PaytmPaymentSettings = PaytmPaymentSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the asynchronous task whose result contains the PDT details
        /// </returns>
        public async Task<string> GetPdtDetailsAsync(string tx)
        {
          
            var url = "https://www.paytm.com";
            var requestContent = new StringContent($"cmd=_notify-synch&at={_PaytmPaymentSettings.PdtToken}&tx={tx}",
                Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);
            var response = await _httpClient.PostAsync(url, requestContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the asynchronous task whose result contains the IPN verification details
        /// </returns>
        public async Task<string> VerifyIpnAsync(string formString)
        {
           
            var url = "https://www.paytm.com";
            var requestContent = new StringContent($"cmd=_notify-validate&{formString}",
                Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);
            var response = await _httpClient.PostAsync(url, requestContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        #endregion
    }
}
