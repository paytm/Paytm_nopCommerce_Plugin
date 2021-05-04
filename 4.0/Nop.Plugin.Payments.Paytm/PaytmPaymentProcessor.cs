using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using Paytm;
namespace Nop.Plugin.Payments.Paytm
{
    /// <summary>
    /// Paytm payment processor
    /// </summary>
    public class PaytmPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly PaytmPaymentSettings _PaytmPaymentSettings;

        #endregion

        #region Ctor

        public PaytmPaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            PaytmPaymentSettings PaytmPaymentSettings)
        {
            this._currencySettings = currencySettings;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;
            this._httpContextAccessor = httpContextAccessor;
            this._localizationService = localizationService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._settingService = settingService;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._PaytmPaymentSettings = PaytmPaymentSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets Paytm URL
        /// </summary>
        /// <returns></returns>
        private string GetPaytmUrl()
        {
            return _PaytmPaymentSettings.PaymentUrl;
        }

        /// <summary>
        /// Gets IPN Paytm URL
        /// </summary>
        /// <returns></returns>
        private string GetIpnPaytmUrl()
        {
            return _PaytmPaymentSettings.PaymentUrl;
        }

        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <param name="values">Values</param>
        /// <param name="response">Response</param>
        /// <returns>Result</returns>
        public bool GetPdtDetails(string tx, out Dictionary<string, string> values, out string response)
        {
            var req = (HttpWebRequest)WebRequest.Create(GetPaytmUrl());
            req.Method = WebRequestMethods.Http.Post;
            req.ContentType = MimeTypes.ApplicationXWwwFormUrlencoded;
            //now Paytm requires user-agent. otherwise, we can get 403 error
            req.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];

            var formContent = $"cmd=_notify-synch&at={_PaytmPaymentSettings.PdtToken}&tx={tx}";
            req.ContentLength = formContent.Length;

            using (var sw = new StreamWriter(req.GetRequestStream(), Encoding.ASCII))
                sw.Write(formContent);

            using (var sr = new StreamReader(req.GetResponse().GetResponseStream()))
                response = WebUtility.UrlDecode(sr.ReadToEnd());

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool firstLine = true, success = false;
            foreach (var l in response.Split('\n'))
            {
                var line = l.Trim();
                if (firstLine)
                {
                    success = line.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
                    firstLine = false;
                }
                else
                {
                    var equalPox = line.IndexOf('=');
                    if (equalPox >= 0)
                        values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
                }
            }

            return success;
        }

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <param name="values">Values</param>
        /// <returns>Result</returns>
        public bool VerifyIpn(string formString, out Dictionary<string, string> values)
        {
            var req = (HttpWebRequest)WebRequest.Create(GetIpnPaytmUrl());
            req.Method = WebRequestMethods.Http.Post;
            req.ContentType = MimeTypes.ApplicationXWwwFormUrlencoded;
            //now Paytm requires user-agent. otherwise, we can get 403 error
            req.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];

            var formContent = $"cmd=_notify-validate&{formString}";
            req.ContentLength = formContent.Length;

            using (var sw = new StreamWriter(req.GetRequestStream(), Encoding.ASCII))
            {
                sw.Write(formContent);
            }

            string response;
            using (var sr = new StreamReader(req.GetResponse().GetResponseStream()))
            {
                response = WebUtility.UrlDecode(sr.ReadToEnd());
            }
            var success = response.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in formString.Split('&'))
            {
                var line = l.Trim();
                var equalPox = line.IndexOf('=');
                if (equalPox >= 0)
                    values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
            }

            return success;
        }

        /// <summary>
        /// Create common query parameters for the request
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Created query parameters</returns>
        /*private IDictionary<string, string> CreateQueryParameters(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();

            //create query parameters
            return new Dictionary<string, string>
            {
                //Paytm ID or an email address associated with your Paytm account
                //["business"] = _PaytmPaymentSettings.BusinessEmail, //mayank

                //the character set and character encoding
                ["charset"] = "utf-8",

                //set return method to "2" (the customer redirected to the return URL by using the POST method, and all payment variables are included)
                ["rm"] = "2",

                ["bn"] = PaytmHelper.NopCommercePartnerCode,
                ["currency_code"] = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode,

                //order identifier
                ["invoice"] = postProcessPaymentRequest.Order.CustomOrderNumber,
                ["custom"] = postProcessPaymentRequest.Order.OrderGuid.ToString(),

                //PDT, IPN and cancel URL
                ["return"] = $"{storeLocation}Plugins/PaymentPaytm/PDTHandler",
                ["notify_url"] = $"{storeLocation}Plugins/PaymentPaytm/IPNHandler",
                ["cancel_return"] = $"{storeLocation}Plugins/PaymentPaytm/CancelOrder",

                //shipping address, if exists
                ["no_shipping"] = postProcessPaymentRequest.Order.ShippingStatus == ShippingStatus.ShippingNotRequired ? "1" : "2",
                ["address_override"] = postProcessPaymentRequest.Order.ShippingStatus == ShippingStatus.ShippingNotRequired ? "0" : "1",
                ["first_name"] = postProcessPaymentRequest.Order.ShippingAddress?.FirstName,
                ["last_name"] = postProcessPaymentRequest.Order.ShippingAddress?.LastName,
                ["address1"] = postProcessPaymentRequest.Order.ShippingAddress?.Address1,
                ["address2"] = postProcessPaymentRequest.Order.ShippingAddress?.Address2,
                ["city"] = postProcessPaymentRequest.Order.ShippingAddress?.City,
                ["state"] = postProcessPaymentRequest.Order.ShippingAddress?.StateProvince?.Abbreviation,
                ["country"] = postProcessPaymentRequest.Order.ShippingAddress?.Country?.TwoLetterIsoCode,
                ["zip"] = postProcessPaymentRequest.Order.ShippingAddress?.ZipPostalCode,
                ["email"] = postProcessPaymentRequest.Order.ShippingAddress?.Email
            };
        }
        */

        /// <summary>
        /// Add order items to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /*private void AddItemsParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //upload order items
            parameters.Add("cmd", "_cart");
            parameters.Add("upload", "1");

            var cartTotal = decimal.Zero;
            var roundedCartTotal = decimal.Zero;
            var itemCount = 1;

            //add shopping cart items
            foreach (var item in postProcessPaymentRequest.Order.OrderItems)
            {
                var roundedItemPrice = Math.Round(item.UnitPriceExclTax, 2);

                //add query parameters
                parameters.Add($"item_name_{itemCount}", item.Product.Name);
                parameters.Add($"amount_{itemCount}", roundedItemPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", item.Quantity.ToString());

                cartTotal += item.PriceExclTax;
                roundedCartTotal += roundedItemPrice * item.Quantity;
                itemCount++;
            }

            //add checkout attributes as order items
            var checkoutAttributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(postProcessPaymentRequest.Order.CheckoutAttributesXml);
            foreach (var attributeValue in checkoutAttributeValues)
            {
                var attributePrice = _taxService.GetCheckoutAttributePrice(attributeValue, false, postProcessPaymentRequest.Order.Customer);
                var roundedAttributePrice = Math.Round(attributePrice, 2);

                //add query parameters
                if (attributeValue.CheckoutAttribute != null)
                {
                    parameters.Add($"item_name_{itemCount}", attributeValue.CheckoutAttribute.Name);
                    parameters.Add($"amount_{itemCount}", roundedAttributePrice.ToString("0.00", CultureInfo.InvariantCulture));
                    parameters.Add($"quantity_{itemCount}", "1");

                    cartTotal += attributePrice;
                    roundedCartTotal += roundedAttributePrice;
                    itemCount++;
                }
            }

            //add shipping fee as a separate order item, if it has price
            var roundedShippingPrice = Math.Round(postProcessPaymentRequest.Order.OrderShippingExclTax, 2);
            if (roundedShippingPrice > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Shipping fee");
                parameters.Add($"amount_{itemCount}", roundedShippingPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.OrderShippingExclTax;
                roundedCartTotal += roundedShippingPrice;
                itemCount++;
            }

            //add payment method additional fee as a separate order item, if it has price
            var roundedPaymentMethodPrice = Math.Round(postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax, 2);
            if (roundedPaymentMethodPrice > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Payment method fee");
                parameters.Add($"amount_{itemCount}", roundedPaymentMethodPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax;
                roundedCartTotal += roundedPaymentMethodPrice;
                itemCount++;
            }

            //add tax as a separate order item, if it has positive amount
            var roundedTaxAmount = Math.Round(postProcessPaymentRequest.Order.OrderTax, 2);
            if (roundedTaxAmount > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Tax amount");
                parameters.Add($"amount_{itemCount}", roundedTaxAmount.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.OrderTax;
                roundedCartTotal += roundedTaxAmount;
                itemCount++;
            }

            if (cartTotal > postProcessPaymentRequest.Order.OrderTotal)
            {
                //get the difference between what the order total is and what it should be and use that as the "discount"
                var discountTotal = Math.Round(cartTotal - postProcessPaymentRequest.Order.OrderTotal, 2);
                roundedCartTotal -= discountTotal;

                //gift card or rewarded point amount applied to cart in nopCommerce - shows in Paytm as "discount"
                parameters.Add("discount_amount_cart", discountTotal.ToString("0.00", CultureInfo.InvariantCulture));
            }

            //save order total that actually sent to Paytm (used for PDT order total validation)
            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, PaytmHelper.OrderTotalSentToPaytm, roundedCartTotal);
        }
        */
        /// <summary>
        /// Add order total to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /*private void AddOrderTotalParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //round order total
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            parameters.Add("cmd", "_xclick");
            parameters.Add("item_name", $"Order Number {postProcessPaymentRequest.Order.CustomOrderNumber}");
            parameters.Add("amount", roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //save order total that actually sent to Paytm (used for PDT order total validation)
            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, PaytmHelper.OrderTotalSentToPaytm, roundedOrderTotal);
        }
        */
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
            result.NewPaymentStatus = Core.Domain.Payments.PaymentStatus.Pending;
            return result;
            //return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //create common query parameters for the request
            var queryParameters = new Dictionary<string, string>();// CreateQueryParameters(postProcessPaymentRequest); //mayank

            //whether to include order items in a transaction
            /*if (_PaytmPaymentSettings.PassProductNamesAndTotals) //mayank
            {
                //add order items query parameters to the request
                var parameters = new Dictionary<string, string>(queryParameters);
                AddItemsParameters(parameters, postProcessPaymentRequest);

                //remove null values from parameters
                parameters = parameters.Where(parameter => !string.IsNullOrEmpty(parameter.Value))
                    .ToDictionary(parameter => parameter.Key, parameter => parameter.Value);

                //ensure redirect URL doesn't exceed 2K chars to avoid "too long URL" exception
                var redirectUrl = QueryHelpers.AddQueryString(GetPaytmUrl(), parameters);
                if (redirectUrl.Length <= 2048)
                {
                    _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                    return;
                }
            }*/

            //or add only an order total query parameters to the request
            string mid, mkey, amount, orderid;
            mid = _PaytmPaymentSettings.MerchantId.Trim().ToString();
            mkey = _PaytmPaymentSettings.MerchantKey.Trim().ToString();
            amount = postProcessPaymentRequest.Order.OrderTotal.ToString("0.00");
            orderid = postProcessPaymentRequest.Order.Id.ToString();
            queryParameters.Add("MID", _PaytmPaymentSettings.MerchantId.Trim().ToString());
            queryParameters.Add("WEBSITE", _PaytmPaymentSettings.Website.Trim().ToString());
            queryParameters.Add("CHANNEL_ID", "WEB");
            queryParameters.Add("INDUSTRY_TYPE_ID", _PaytmPaymentSettings.IndustryTypeId.Trim().ToString());
            queryParameters.Add("TXN_AMOUNT", postProcessPaymentRequest.Order.OrderTotal.ToString("0.00"));
            queryParameters.Add("ORDER_ID", postProcessPaymentRequest.Order.Id.ToString());
            queryParameters.Add("EMAIL", postProcessPaymentRequest.Order.BillingAddress.Email);
            queryParameters.Add("MOBILE_NO", postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);
            queryParameters.Add("CUST_ID", postProcessPaymentRequest.Order.BillingAddress.Email);
            if (_PaytmPaymentSettings.UseDefaultCallBack)
            {
                queryParameters.Add("CALLBACK_URL", _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaytm/Return");
            }
            else
            {
                queryParameters.Add("CALLBACK_URL", _PaytmPaymentSettings.CallBackUrl.Trim());
            }
            //queryParameters.Add("CHECKSUMHASH",
            //             paytm.CheckSum.generateCheckSum(_PaytmPaymentSettings.MerchantKey, queryParameters));
            queryParameters.Add("CHECKSUMHASH",
                      Checksum.generateSignature(queryParameters,_PaytmPaymentSettings.MerchantKey));
            //AddOrderTotalParameters(queryParameters, postProcessPaymentRequest);
            string txntoken = GetTxnToken(amount, mid, orderid, mkey);
            //remove null values from parameters
            queryParameters = queryParameters.Where(parameter => !string.IsNullOrEmpty(parameter.Value))
                .ToDictionary(parameter => parameter.Key, parameter => parameter.Value);
            string scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            string host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            _httpContextAccessor.HttpContext.Response.Cookies.Append("token", txntoken);
            _httpContextAccessor.HttpContext.Response.Cookies.Append("orderid", orderid);
            _httpContextAccessor.HttpContext.Response.Cookies.Append("amount", amount);
            _httpContextAccessor.HttpContext.Response.Cookies.Append("mid", mid);
            var url = QueryHelpers.AddQueryString(GetPaytmUrl(), queryParameters);
            var absoluteUri = string.Concat(scheme, "://", host, "/Plugins/PaymentPaytm/JSCheckoutView");
            _httpContextAccessor.HttpContext.Response.Redirect(absoluteUri);
          
        }
        private string GetTxnToken(string amount, string mid, string orderid, string mkey)
        {
            APIResponse apiresponse = new APIResponse();
            Dictionary<string, object> body = new Dictionary<string, object>();
            Dictionary<string, string> head = new Dictionary<string, string>();
            Dictionary<string, object> requestBody = new Dictionary<string, object>();

            Dictionary<string, string> txnAmount = new Dictionary<string, string>();
            string scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            string host = _httpContextAccessor.HttpContext.Request.Host.ToString();
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
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
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
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            /*return this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _PaytmPaymentSettings.AdditionalFee, _PaytmPaymentSettings.AdditionalFeePercentage);*///mayank

            return this.CalculateAdditionalFee(_orderTotalCalculationService, cart,0,false); 
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPaytm/Configure";
        }

        /// <summary>
        /// Gets a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <param name="viewComponentName">View component name</param>
        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "PaymentPaytm";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new PaytmPaymentSettings() //mayank
            {
                MerchantId = "",
                MerchantKey = "",
                Website = "",
                IndustryTypeId = "",
                PaymentUrl = "",
                CallBackUrl = _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaytm/Return",
                TxnStatusUrl = "",
                UseDefaultCallBack = true
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.RedirectionTip", "You will be redirected to Paytm site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantId", "Merchant ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantId.Hint", "Enter merchant ID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack", "Default CallBack");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack.Hint", "Uncheck and use customized CallBack Url in below field.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey", "Merchant Key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey.Hint", "Enter Merchant key.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Website", "Website");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Website.Hint", "Enter website param.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId", "Industry Type Id");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId.Hint", "Enter Industry Type Id.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl", "Payment URL");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl.Hint", "Select payment url.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl", "Callback URL");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl.Hint", "Enter call back url.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl", "TxnStatus URL");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl.Hint", "Enter TxnStatus back url.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentMethodDescription", "Pay by Paytm Wallet / credit / debit card / Net Banking");
            base.Install();

            /*_settingService.SaveSetting(new PaytmPaymentSettings
            {
                UseSandbox = true
            });

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.BusinessEmail", "Business Email");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.BusinessEmail.Hint", "Specify your Paytm business email.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.PassProductNamesAndTotals", "Pass product names and order totals to Paytm");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.PassProductNamesAndTotals.Hint", "Check if product names and order totals should be passed to Paytm.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.PDTToken", "PDT Identity Token");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.PDTToken.Hint", "Specify PDT identity token");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.RedirectionTip", "You will be redirected to Paytm site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Instructions", "<p><b>If you're using this gateway ensure that your primary store currency is supported by Paytm.</b><br /><br />To use PDT, you must activate PDT and Auto Return in your Paytm account profile. You must also acquire a PDT identity token, which is used in all PDT communication you send to Paytm. Follow these steps to configure your account for PDT:<br /><br />1. Log in to your Paytm account (click <a href=\"https://www.Paytm.com/us/webapps/mpp/referral/Paytm-business-account2?partner_id=9JJPJNNPQ7PZ8\" target=\"_blank\">here</a> to create your account).<br />2. Click the Profile subtab.<br />3. Click Website Payment Preferences in the Seller Preferences column.<br />4. Under Auto Return for Website Payments, click the On radio button.<br />5. For the Return URL, enter the URL on your site that will receive the transaction ID posted by Paytm after a customer payment ({0}).<br />6. Under Payment Data Transfer, click the On radio button.<br />7. Click Save.<br />8. Click Website Payment Preferences in the Seller Preferences column.<br />9. Scroll down to the Payment Data Transfer section of the page to view your PDT identity token.<br /><br /></p>");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentMethodDescription", "You will be redirected to Paytm site to complete the payment");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.RoundingWarning", "It looks like you have \"ShoppingCartSettings.RoundPricesDuringCalculation\" setting disabled. Keep in mind that this can lead to a discrepancy of the order total amount, as Paytm only rounds to two decimals.");

            base.Install();*/
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PaytmPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack.Hint");
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
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Paytm.PaymentMethodDescription");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
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
            //for example, for a redirection payment method, description may be like this: "You will be redirected to Paytm site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.Paytm.PaymentMethodDescription"); }
        }

        #endregion
    }
}
