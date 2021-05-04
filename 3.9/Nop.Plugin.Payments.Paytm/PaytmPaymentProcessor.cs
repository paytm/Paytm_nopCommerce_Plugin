using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Paytm.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Framework;
using Paytm;

//using CCA.Util;
using System.Collections.Specialized;
using System.Web;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Nop.Plugin.Payments.Paytm
{
    /// <summary>
    /// Paytm payment processor
    /// </summary>
    public class PaytmPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields
      
        private readonly PaytmPaymentSettings _PaytmPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        //CCACrypto ccaCrypto = new CCACrypto();
        #endregion

        #region Ctor

        public PaytmPaymentProcessor(PaytmPaymentSettings PaytmPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper, ILocalizationService localizationService)
        {
            this._PaytmPaymentSettings = PaytmPaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
            this._localizationService = localizationService;
        }

        #endregion

        #region Utilities

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }


        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
			//string amount = postProcessPaymentRequest.Order.OrderTotal.ToString ("#.##");
			//amount.ToString ();
            var remotePostHelper = new RemotePost();
            var remotePostHelperData = new Dictionary<string, string>();
            var getUrlData = HttpContext.Current.Request;
            string scheme = getUrlData.Url.Scheme;
            string host = getUrlData.Url.Authority;
            
            var absoluteUri = string.Concat(scheme, "://", host, "/Plugins/PaymentPaytm/JSCheckoutView");
           
            string mid, mkey, amount, orderid;
            mid = _PaytmPaymentSettings.MerchantId.Trim().ToString();
            mkey = _PaytmPaymentSettings.MerchantKey.Trim().ToString();
            amount = postProcessPaymentRequest.Order.OrderTotal.ToString("0.00");
            orderid = postProcessPaymentRequest.Order.Id.ToString();
            remotePostHelper.FormName = "PaytmForm";
			//remotePostHelper.Url = _PaytmPaymentSettings.PaymentUrl;
            remotePostHelper.Url = absoluteUri;
            remotePostHelperData.Add("MID", _PaytmPaymentSettings.MerchantId.ToString());
			remotePostHelperData.Add("WEBSITE", _PaytmPaymentSettings.Website.ToString());
			remotePostHelperData.Add("CHANNEL_ID", "WEB");
			remotePostHelperData.Add("INDUSTRY_TYPE_ID", _PaytmPaymentSettings.IndustryTypeId.ToString());
			remotePostHelperData.Add("TXN_AMOUNT", postProcessPaymentRequest.Order.OrderTotal.ToString ("0.00"));
            remotePostHelperData.Add("ORDER_ID",  postProcessPaymentRequest.Order.Id.ToString());
			remotePostHelperData.Add("EMAIL", postProcessPaymentRequest.Order.BillingAddress.Email);
            remotePostHelperData.Add("MOBILE_NO", postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);
            remotePostHelperData.Add("CUST_ID", postProcessPaymentRequest.Order.BillingAddress.Email);
			remotePostHelperData.Add("CALLBACK_URL", _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaytm/Return");
            string txntoken = GetTxnToken(amount, mid, orderid, mkey);
            HttpContext.Current.Response.Cookies.Add(new HttpCookie("amount", amount));
            HttpContext.Current.Response.Cookies.Add(new HttpCookie("mid", mid));
            HttpContext.Current.Response.Cookies.Add(new HttpCookie("mkey", mkey));
            HttpContext.Current.Response.Cookies.Add(new HttpCookie("orderid", orderid));
            HttpContext.Current.Response.Cookies.Add(new HttpCookie("token", txntoken));

            // changes done by mayank -- 
            Dictionary<string,string> parameters = new Dictionary<string,string> ();

            foreach (var item in remotePostHelperData.Keys)
            {
                // below code snippet is mandatory, so that no one can use your checksumgeneration url for other purpose .
                if (remotePostHelperData[item].Trim().ToUpper().Contains("REFUND") || remotePostHelperData[item].Trim().Contains("|"))
                {
                    continue;
                }
                else
                {
                    parameters.Add(item, remotePostHelperData[item]);
                    remotePostHelper.Add(item, remotePostHelperData[item]);
                }
            }
            

            // changes end -- 
            try
            {
                string checksumHash = "";

				//checksumHash = CheckSum.generateCheckSum(_PaytmPaymentSettings.MerchantKey,parameters);
                checksumHash = Checksum.generateSignature(parameters,_PaytmPaymentSettings.MerchantKey);
                remotePostHelper.Add("CHECKSUMHASH", checksumHash);
                remotePostHelper.Post();
            }
            catch (Exception ep)
            {
                throw new Exception(ep.Message);
            }
        }

        private string GetTxnToken(string amount, string mid, string orderid, string mkey)
        {
            APIResponse apiresponse = new APIResponse();
            Dictionary<string, object> body = new Dictionary<string, object>();
            Dictionary<string, string> head = new Dictionary<string, string>();
            Dictionary<string, object> requestBody = new Dictionary<string, object>();

            Dictionary<string, string> txnAmount = new Dictionary<string, string>();
            var getUrlData = HttpContext.Current.Request;
            string scheme = getUrlData.Url.Scheme;
            string host = getUrlData.Url.Authority;
            string displayToken = string.Empty;
            txnAmount.Add("value", amount);
            txnAmount.Add("currency", "INR");
            Dictionary<string, string> userInfo = new Dictionary<string, string>();
            userInfo.Add("custId", "cust_001");
            body.Add("requestType", "Payment");
            body.Add("mid", mid);
            body.Add("websiteName", _PaytmPaymentSettings.Website.Trim().ToString());
            body.Add("orderId", orderid);
            body.Add("txnAmount", txnAmount);
            body.Add("userInfo", userInfo);
           body.Add("callbackUrl", string.Concat(scheme, "://", host, "/Plugins/PaymentPaytm/Return"));

            /*
            * Generate checksum by parameters we have in body
            * Find your Merchant Key in your Paytm Dashboard at https://dashboard.paytm.com/next/apikeys 
            */

            string paytmChecksum = Checksum.generateSignature(JsonConvert.SerializeObject(body), mkey);

            head.Add("signature", paytmChecksum);

            requestBody.Add("body", body);
            requestBody.Add("head", head);

            string post_data = JsonConvert.SerializeObject(requestBody);
            string url = string.Empty;
            if (_PaytmPaymentSettings.env == "Stage")
            {
                //For  Staging
                url = "https://securegw-stage.paytm.in/theia/api/v1/initiateTransaction?mid=" + mid + "&orderId=" + orderid + " ";
            }
            if (_PaytmPaymentSettings.env == "Prod")
            {
                //For  Production 
                url = "https://securegw.paytm.in/theia/api/v1/initiateTransaction?mid=" + mid + "&orderId=" + orderid + "";
            }
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

            webRequest.Method = "POST";
            webRequest.ContentType = "application/json";
            webRequest.ContentLength = post_data.Length;

            using (StreamWriter requestWriter = new StreamWriter(webRequest.GetRequestStream()))
            {
                requestWriter.Write(post_data);
            }

            string responseData = string.Empty;

            using (StreamReader responseReader = new StreamReader(webRequest.GetResponse().GetResponseStream()))
            {
                responseData = responseReader.ReadToEnd();
                apiresponse = JsonConvert.DeserializeObject<APIResponse>(responseData);
                JObject jObject = JObject.Parse(responseData);
                displayToken = jObject.SelectToken("body.txnToken").Value<string>();
                //  Console.WriteLine(responseData);
            }

            return displayToken;
        }
        public class Head
        {
            public string responseTimestamp;
            public string version;
            public string signature;
        }
        public class ResultInfo
        {
            public string resultStatus;
            public string resultCode;
            public string resultMsg;
        }
        public class Body
        {
            public ResultInfo resultInfo;
            public string txnToken;
            public string isPromoCodeValid;
            public string authenticated;
        }
        public class APIResponse
        {
            public Head head { get; set; }
            public Body body { get; set; }

        }
        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /*public void PostProcessPaymentOld(PostProcessPaymentRequest postProcessPaymentRequest)
        {
			var remotePostHelper = new RemotePost();
			var remotePostHelperData = new Dictionary<string, string>();

			remotePostHelper.FormName = "PaytmForm";
			remotePostHelper.Url = _PaytmPaymentSettings.PaymentUrl;
			remotePostHelperData.Add("MID", _PaytmPaymentSettings.MerchantId.ToString());
			remotePostHelperData.Add("WEBSITE", _PaytmPaymentSettings.Website.ToString());
			remotePostHelperData.Add("INDUSTRY_TYPE_ID", _PaytmPaymentSettings.IndustryTypeId.ToString());
			remotePostHelperData.Add("TXN_AMOUNT", postProcessPaymentRequest.Order.OrderTotal.ToString("#.##"));
			remotePostHelperData.Add("CHANNEL_ID", "WEB");
			remotePostHelperData.Add("ORDER_ID", postProcessPaymentRequest.Order.Id.ToString());
			remotePostHelperData.Add("EMAIL", postProcessPaymentRequest.Order.BillingAddress.Email);
			remotePostHelperData.Add("MOBILE_NO", postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);
			remotePostHelperData.Add("CUST_ID", postProcessPaymentRequest.Order.BillingAddress.Email);
			remotePostHelperData.Add("CALLBACK_URL", _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaytm/Return");
			//remotePostHelperData.Add("CALLBACK_URL", _PaytmPaymentSettings.CallBackUrl.ToString());


			Dictionary<string,string> parameters = new Dictionary<string,string> ();


			foreach (var item in remotePostHelperData)
			{
				parameters.Add(item.Key,item.Value);
			}

			try
			{
				string checksumHash = "";

				checksumHash = CheckSum.generateCheckSum(_PaytmPaymentSettings.MerchantKey,parameters);
				remotePostHelper.Add("CHECKSUMHASH", checksumHash);


				remotePostHelper.Post();
			}
			catch (Exception ep)
			{
				throw new Exception(ep.Message);
			}
        }
        */
        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //Paytm is the redirection payment method
            //It also validates whether order is also paid (after redirection) so customers will not be able to pay twice

            //payment status should be Pending
            if (order.PaymentStatus != PaymentStatus.Pending)
                return false;

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPaytm";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Paytm.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPaytm";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Paytm.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentPaytmController);
        }

        public override void Install()
        {
            var settings = new PaytmPaymentSettings()
            {
                MerchantId = "",
                MerchantKey = "",
                Website = "",
                IndustryTypeId = "",
                PaymentUrl = "",
                CallBackUrl = "",
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.RedirectionTip", "You will be redirected to Paytm site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantId", "Merchant ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantId.Hint", "Enter merchant ID.");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey", "Merchant Key");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey.Hint", "Enter Merchant key.");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Website", "Website");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Website.Hint", "Enter website param.");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId", "Industry Type Id");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId.Hint", "Enter Industry Type Id.");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl", "Payment URL");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl.Hint", "Select payment url.");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl", "Callback url URL");
			this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl.Hint", "enter call back url.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentMethodDescription", "Pay by Paytm Wallet / credit / debit card / Net Banking");
            base.Install();
        }

        public override void Uninstall()
        {
            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantId.Hint");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey.Hint");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.Website");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.Website.Hint");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId.Hint");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl.Hint");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl");
			this.DeletePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.PaymentMethodDescription");

            base.Uninstall();
        }
        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        public bool SkipPaymentInfo
        {
            get { return false; }
        }

         /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.Paytm.PaymentMethodDescription"); }
        }

        #endregion
    }
}
