using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;
using Microsoft.Net.Http.Headers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Payments.Paytm.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tax;
using Paytm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly PaytmHttpClient _paytmHttpClient;
        private readonly PaytmPaymentSettings _paytmPaymentSettings;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        #endregion

        #region Ctor

        public PaytmPaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            PaytmHttpClient paytmHttpClient,
            IOrderTotalCalculationService orderTotalCalculationService,
            PaytmPaymentSettings paytmPaymentSettings)
        {
            _currencySettings = currencySettings;
            _checkoutAttributeParser = checkoutAttributeParser;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _taxService = taxService;
            _webHelper = webHelper;
            _paytmHttpClient = paytmHttpClient;
            _paytmPaymentSettings = paytmPaymentSettings;
            _orderTotalCalculationService = orderTotalCalculationService;
        }

        #endregion

        #region Utilities
        /// <summary>
        /// Gets Paytm URL
        /// </summary>
        /// <returns></returns>
        private string GetPaytmUrl()
        {
            return _paytmPaymentSettings.PaymentUrl;
        }

        /// <summary>
        /// Gets IPN Paytm URL
        /// </summary>
        /// <returns></returns>
        private string GetIpnPaytmUrl()
        {
            return _paytmPaymentSettings.PaymentUrl;
        }
        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <param name="values">Values</param>
        /// <param name="response">Response</param>
        /// <returns>Result</returns>
        /// 
        //#region   Get Pdt Paypal
        ////////
        //public bool GetPdtDetails(string tx, out Dictionary<string, string> values, out string response)
        //{

        //    var req = (HttpWebRequest)WebRequest.Create(GetPaytmUrl());
        //    req.Method = WebRequestMethods.Http.Post;
        //    req.ContentType = MimeTypes.ApplicationXWwwFormUrlencoded;
        //    //now Paytm requires user-agent. otherwise, we can get 403 error
        //   req.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];

        //    var formContent = $"cmd=_notify-synch&at={_paytmPaymentSettings.PdtToken}&tx={tx}";
        //    req.ContentLength = formContent.Length;

        //    //using (var sw = new streamwriter(req.getrequeststream(), encoding.ascii))
        //    //    sw.write(formcontent);

        //    //using (var sr = new streamreader(req.getresponse().getresponsestream()))
        //    //    response = webutility.urldecode(sr.readtoend());

        //    response = WebUtility.UrlDecode(_paytmHttpClient.GetPdtDetailsAsync(tx).Result);

        //    values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        //    bool firstLine = true, success = false;
        //    foreach (var l in response.Split('\n'))
        //    {
        //        var line = l.Trim();
        //        if (firstLine)
        //        {
        //            success = line.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
        //            firstLine = false;
        //        }
        //        else
        //        {
        //            var equalPox = line.IndexOf('=');
        //            if (equalPox >= 0)
        //                values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
        //        }
        //    }

        //    return success;
        //}

        //#endregion

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <param name="values">Values</param>
        /// <returns>Result</returns>

        //#region Verify Pin Paypal
        //public bool VerifyIpn(string formString, out Dictionary<string, string> values)
        //{


        //     var req = (HttpWebRequest)WebRequest.Create(GetIpnPaytmUrl());
        //    req.Method = WebRequestMethods.Http.Post;
        //    req.ContentType = MimeTypes.ApplicationXWwwFormUrlencoded;
        //    //now Paytm requires user-agent. otherwise, we can get 403 error
        //    req.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];

        //    var formContent = $"cmd=_notify-validate&{formString}";
        //    req.ContentLength = formContent.Length;

        //    using (var sw = new StreamWriter(req.GetRequestStream(), Encoding.ASCII))
        //    {
        //        sw.Write(formContent);
        //    }

        //    string responses;
        //    using (var sr = new StreamReader(req.GetResponse().GetResponseStream()))
        //    {
        //        responses = WebUtility.UrlDecode(sr.ReadToEnd());
        //    }
        //    //var success = responses.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

        //    var response = WebUtility.UrlDecode(_paytmHttpClient.VerifyIpnAsync(formString).Result);
        //    var success = response.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

        //    values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        //    foreach (var l in formString.Split('&'))
        //    {
        //        var line = l.Trim();
        //        var equalPox = line.IndexOf('=');
        //        if (equalPox >= 0)
        //            values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
        //    }

        //    return success;
        //}
        //#endregion

        /// <summary>
        /// Create common query parameters for the request
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Created query parameters</returns>
        /// 

        public bool GetPdtDetails(string tx, out Dictionary<string, string> values, out string response)
        {
            var req = (HttpWebRequest)WebRequest.Create(GetPaytmUrl());
            req.Method = WebRequestMethods.Http.Post;
            req.ContentType = MimeTypes.ApplicationXWwwFormUrlencoded;
            //now Paytm requires user-agent. otherwise, we can get 403 error
            req.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];

            var formContent = $"cmd=_notify-synch&at={_paytmPaymentSettings.PdtToken}&tx={tx}";
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

        private IDictionary<string, string> CreateQueryParameters(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();

            //choosing correct order address
            var orderAddress = postProcessPaymentRequest.Order.PickupInStore
                    ? postProcessPaymentRequest.Order.PickupAddress
                    : postProcessPaymentRequest.Order.ShippingAddress;

            //create query parameters
            return new Dictionary<string, string>
            {
                //Paytm ID or an email address associated with your Paytm account
            //    ["business"] = _paytmPaymentSettings.BusinessEmail,

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
                ["first_name"] = orderAddress?.FirstName,
                ["last_name"] = orderAddress?.LastName,
                ["address1"] = orderAddress?.Address1,
                ["address2"] = orderAddress?.Address2,
                ["city"] = orderAddress?.City,
                ["state"] = orderAddress?.StateProvince?.Abbreviation,
                ["country"] = orderAddress?.Country?.TwoLetterIsoCode,
                ["zip"] = orderAddress?.ZipPostalCode,
                ["email"] = orderAddress?.Email
            };
        }

        /// <summary>
        /// Add order items to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private void AddItemsParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
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
                if (attributeValue.CheckoutAttribute == null) 
                    continue;

                parameters.Add($"item_name_{itemCount}", attributeValue.CheckoutAttribute.Name);
                parameters.Add($"amount_{itemCount}", roundedAttributePrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += attributePrice;
                roundedCartTotal += roundedAttributePrice;
                itemCount++;
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

        /// <summary>
        /// Add order total to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private void AddOrderTotalParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //round order total
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            parameters.Add("cmd", "_xclick");
            parameters.Add("item_name", $"Order Number {postProcessPaymentRequest.Order.CustomOrderNumber}");
            parameters.Add("amount", roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //save order total that actually sent to Paytm (used for PDT order total validation)
            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, PaytmHelper.OrderTotalSentToPaytm, roundedOrderTotal);
        }

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
            mid = _paytmPaymentSettings.MerchantId.Trim().ToString();
            mkey = _paytmPaymentSettings.MerchantKey.Trim().ToString();
            amount = postProcessPaymentRequest.Order.OrderTotal.ToString("0.00");
            orderid = postProcessPaymentRequest.Order.Id.ToString();
            queryParameters.Add("MID", _paytmPaymentSettings.MerchantId.Trim().ToString());
            queryParameters.Add("WEBSITE", _paytmPaymentSettings.Website.Trim().ToString());
            queryParameters.Add("CHANNEL_ID", "WEB");
            queryParameters.Add("INDUSTRY_TYPE_ID", _paytmPaymentSettings.IndustryTypeId.Trim().ToString());
            queryParameters.Add("TXN_AMOUNT", postProcessPaymentRequest.Order.OrderTotal.ToString("0.00"));
            queryParameters.Add("ORDER_ID", postProcessPaymentRequest.Order.Id.ToString());
            queryParameters.Add("EMAIL", postProcessPaymentRequest.Order.BillingAddress.Email);
            queryParameters.Add("MOBILE_NO", postProcessPaymentRequest.Order.BillingAddress.PhoneNumber);
            queryParameters.Add("CUST_ID", postProcessPaymentRequest.Order.BillingAddress.Email);
            if (_paytmPaymentSettings.UseDefaultCallBack)
            {
                queryParameters.Add("CALLBACK_URL", _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaytm/Return");
            }
            else
            {
                queryParameters.Add("CALLBACK_URL", _paytmPaymentSettings.CallBackUrl.Trim());
            }
            //queryParameters.Add("CHECKSUMHASH",
            //             paytm.CheckSum.generateCheckSum(_paytmPaymentSettings.MerchantKey, queryParameters));
            queryParameters.Add("CHECKSUMHASH",
                      Checksum.generateSignature(queryParameters, _paytmPaymentSettings.MerchantKey));
            string txntoken = GetTxnToken(amount, mid, orderid, mkey);
            //AddOrderTotalParameters(queryParameters, postProcessPaymentRequest);

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
            //  _httpContextAccessor.HttpContext.Response.Redirect(url);
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
            body.Add("websiteName", _paytmPaymentSettings.Website.Trim().ToString());
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
            if (_paytmPaymentSettings.env == "Stage")
            {
                //For  Staging
                url = "https://securegw-stage.paytm.in/theia/api/v1/initiateTransaction?mid=" + mid + "&orderId=" + orderid + " ";
            }
            if (_paytmPaymentSettings.env == "Prod")
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
            return _paymentService.CalculateAdditionalFee(cart,
                _paytmPaymentSettings.AdditionalFee, _paytmPaymentSettings.AdditionalFeePercentage);
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
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPaytm/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentPaytm";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            //by sahil
            var settings = new PaytmPaymentSettings() //sahil
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
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.RedirectionTip", "You will be redirected to Paytm site to complete the order.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantId", "Merchant ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantId.Hint", "Enter merchant ID.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack", "Default CallBack");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack.Hint", "Uncheck and use customized CallBack Url in below field.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey", "Merchant Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey.Hint", "Enter Merchant key.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Website", "Website");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.Website.Hint", "Enter website param.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId", "Industry Type Id");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId.Hint", "Enter Industry Type Id.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl", "Payment URL");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl.Hint", "Select payment url.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl", "Callback URL");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl.Hint", "Enter call back url.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl", "TxnStatus URL");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl.Hint", "Enter TxnStatus back url.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Paytm.PaymentMethodDescription", "Pay by Paytm Wallet / credit / debit card / Net Banking");
            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PaytmPaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.UseDefaultCallBack.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.MerchantKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.Website");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.Website.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.IndustryTypeId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.PaymentUrl.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.CallBackUrl.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.TxnStatusUrl.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Paytm.PaymentMethodDescription");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => false;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => false;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => false;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => false;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.Paytm.PaymentMethodDescription");

        #endregion
    }
}