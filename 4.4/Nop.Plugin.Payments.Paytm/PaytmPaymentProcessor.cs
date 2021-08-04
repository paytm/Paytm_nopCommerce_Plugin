using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Payments.Paytm.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tax;
using Paytm;
namespace Nop.Plugin.Payments.Paytm
{
    public class PaytmPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly IAddressService _addressService;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly PaytmHttpClient _PaytmHttpClient;
        private readonly PaytmPaymentSettings _PaytmPaymentSettings;

        #endregion

        #region Ctor

        public PaytmPaymentProcessor(CurrencySettings currencySettings,
            IAddressService addressService,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICountryService countryService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IOrderService orderService,
            IPaymentService paymentService,
            IProductService productService,
            ISettingService settingService,
            IStateProvinceService stateProvinceService,
            ITaxService taxService,
            IWebHelper webHelper,
            PaytmHttpClient PaytmHttpClient,
            PaytmPaymentSettings PaytmPaymentSettings)
        {
            _currencySettings = currencySettings;
            _addressService = addressService;
            _checkoutAttributeParser = checkoutAttributeParser;
            _countryService = countryService;
            _currencyService = currencyService;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _orderService = orderService;
            _paymentService = paymentService;
            _productService = productService;
            _settingService = settingService;
            _stateProvinceService = stateProvinceService;
            _taxService = taxService;
            _webHelper = webHelper;
            _PaytmHttpClient = PaytmHttpClient;
            _PaytmPaymentSettings = PaytmPaymentSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Create common query parameters for the request
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the created query parameters
        /// </returns>
        private async Task<IDictionary<string, string>> CreateQueryParametersAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();

            //choosing correct order address
            var orderAddress = await _addressService.GetAddressByIdAsync(
                (postProcessPaymentRequest.Order.PickupInStore ? postProcessPaymentRequest.Order.PickupAddressId : postProcessPaymentRequest.Order.ShippingAddressId) ?? 0);

            //create query parameters
            return new Dictionary<string, string>
            {

                //the character set and character encoding
                ["charset"] = "utf-8",

                //set return method to "2" (the customer redirected to the return URL by using the POST method, and all payment variables are included)
                ["rm"] = "2",

                ["bn"] = PaytmHelper.NopCommercePartnerCode,
                ["currency_code"] = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode,

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
                ["state"] = (await _stateProvinceService.GetStateProvinceByAddressAsync(orderAddress))?.Abbreviation,
                ["country"] = (await _countryService.GetCountryByAddressAsync(orderAddress))?.TwoLetterIsoCode,
                ["zip"] = orderAddress?.ZipPostalCode,
                ["email"] = orderAddress?.Email
            };
        }

        /// <summary>
        /// Add order items to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task AddItemsParametersAsync(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //upload order items
            parameters.Add("cmd", "_cart");
            parameters.Add("upload", "1");

            var cartTotal = decimal.Zero;
            var roundedCartTotal = decimal.Zero;
            var itemCount = 1;

            //add shopping cart items
            foreach (var item in await _orderService.GetOrderItemsAsync(postProcessPaymentRequest.Order.Id))
            {
                var roundedItemPrice = Math.Round(item.UnitPriceExclTax, 2);

                var product = await _productService.GetProductByIdAsync(item.ProductId);

                //add query parameters
                parameters.Add($"item_name_{itemCount}", product.Name);
                parameters.Add($"amount_{itemCount}", roundedItemPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", item.Quantity.ToString());

                cartTotal += item.PriceExclTax;
                roundedCartTotal += roundedItemPrice * item.Quantity;
                itemCount++;
            }

            //add checkout attributes as order items
            var checkoutAttributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(postProcessPaymentRequest.Order.CheckoutAttributesXml);
            var customer = await _customerService.GetCustomerByIdAsync(postProcessPaymentRequest.Order.CustomerId);

            await foreach (var (attribute, values) in checkoutAttributeValues)
            {
                await foreach (var attributeValue in values)
                {
                    var (attributePrice, _) = await _taxService.GetCheckoutAttributePriceAsync(attribute, attributeValue, false, customer);
                    var roundedAttributePrice = Math.Round(attributePrice, 2);

                    //add query parameters
                    if (attribute == null)
                        continue;

                    parameters.Add($"item_name_{itemCount}", attribute.Name);
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
            await _genericAttributeService.SaveAttributeAsync(postProcessPaymentRequest.Order, PaytmHelper.OrderTotalSentToPaytm, roundedCartTotal);
        }

        /// <summary>
        /// Add order total to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task AddOrderTotalParametersAsync(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //round order total
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            parameters.Add("cmd", "_xclick");
            parameters.Add("item_name", $"Order Number {postProcessPaymentRequest.Order.CustomOrderNumber}");
            parameters.Add("amount", roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //save order total that actually sent to Paytm (used for PDT order total validation)
            await _genericAttributeService.SaveAttributeAsync(postProcessPaymentRequest.Order, PaytmHelper.OrderTotalSentToPaytm, roundedOrderTotal);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result, Values
        /// </returns>
        public async Task<(bool result, Dictionary<string, string> values)> VerifyIpnAsync(string formString)
        {
            var response = WebUtility.UrlDecode(await _PaytmHttpClient.VerifyIpnAsync(formString));
            var success = response.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in formString.Split('&'))
            {
                var line = l.Trim();
                var equalPox = line.IndexOf('=');
                if (equalPox >= 0)
                    values.Add(line[0..equalPox], line[(equalPox + 1)..]);
            }

            return (success, values);
        }

        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result, Values, Response
        /// </returns>
        public async Task<(bool result, Dictionary<string, string> values, string response)> GetPdtDetailsAsync(string tx)
        {
            var response = WebUtility.UrlDecode(await _PaytmHttpClient.GetPdtDetailsAsync(tx));

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                        values.Add(line[0..equalPox], line[(equalPox + 1)..]);
                }
            }

            return (success, values, response);
        }


        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the process payment result
        /// </returns>
        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return Task.FromResult(new ProcessPaymentResult());
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
          
            var queryParameters = await CreateQueryParametersAsync(postProcessPaymentRequest);

            //whether to include order items in a transaction
           // if (_PaytmPaymentSettings.PassProductNamesAndTotals)
            //{
                //add order items query parameters to the request
                var parameters = new Dictionary<string, string>(queryParameters);
            string mid, mkey, amount, orderid;
            mid = _PaytmPaymentSettings.MerchantId.Trim().ToString();
            mkey = _PaytmPaymentSettings.MerchantKey.Trim().ToString();
            amount = postProcessPaymentRequest.Order.OrderTotal.ToString("0.00");
            orderid = postProcessPaymentRequest.Order.Id.ToString();
            parameters.Add("MID", _PaytmPaymentSettings.MerchantId.Trim().ToString());
            parameters.Add("WEBSITE", _PaytmPaymentSettings.Website.Trim().ToString());
            parameters.Add("CHANNEL_ID", "WEB");
            parameters.Add("INDUSTRY_TYPE_ID", _PaytmPaymentSettings.IndustryTypeId.Trim().ToString());
            parameters.Add("TXN_AMOUNT", postProcessPaymentRequest.Order.OrderTotal.ToString("0.00"));
            parameters.Add("ORDER_ID", postProcessPaymentRequest.Order.Id.ToString());
            parameters.Add("EMAIL", postProcessPaymentRequest.Order.CustomerId.ToString());
            parameters.Add("CUST_ID", postProcessPaymentRequest.Order.CustomerId.ToString());

            if (_PaytmPaymentSettings.UseDefaultCallBack)
            {
                parameters.Add("CALLBACK_URL", _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaytm/Return");
            }
            else
            {
                parameters.Add("CALLBACK_URL", _PaytmPaymentSettings.CallBackUrl.Trim());
            }

            parameters.Add("CHECKSUMHASH",
                         Checksum.generateSignature(parameters, _PaytmPaymentSettings.MerchantKey));
            string domainname = _httpContextAccessor.HttpContext.Request.Host.Value;
            string txntoken = GetTxnToken(amount, mid, orderid, mkey);

            await AddItemsParametersAsync(parameters, postProcessPaymentRequest);

                //remove null values from parameters
                parameters = parameters.Where(parameter => !string.IsNullOrEmpty(parameter.Value))
                    .ToDictionary(parameter => parameter.Key, parameter => parameter.Value);
            string scheme = _httpContextAccessor.HttpContext.Request.Scheme;
            string host = _httpContextAccessor.HttpContext.Request.Host.ToString();
            string controllername = _httpContextAccessor.HttpContext.Request.RouteValues["controller"].ToString();
            string path = _httpContextAccessor.HttpContext.Request.Path;
            string result = scheme + host + controllername;
            _httpContextAccessor.HttpContext.Response.Cookies.Append("token", txntoken);
            _httpContextAccessor.HttpContext.Response.Cookies.Append("orderid", orderid);
            _httpContextAccessor.HttpContext.Response.Cookies.Append("amount", amount);
            _httpContextAccessor.HttpContext.Response.Cookies.Append("mid", mid);
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the rue - hide; false - display.
        /// </returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the additional handling fee
        /// </returns>
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return await _paymentService.CalculateAdditionalFeeAsync(cart,
                _PaytmPaymentSettings.AdditionalFee, _PaytmPaymentSettings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the capture payment result
        /// </returns>
        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the process payment result
        /// </returns>
        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of validating errors
        /// </returns>
        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the payment info holder
        /// </returns>
        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
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
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new PaytmPaymentSettings
            {
               // UseSandbox = true
            });

            //locales
            await _localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.Paytm.Fields.AdditionalFee"] = "Additional fee",
                ["Plugins.Payments.Paytm.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
                ["Plugins.Payments.Paytm.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
                ["Plugins.Payments.Paytm.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
                ["Plugins.Payments.Paytm.Fields.BusinessEmail"] = "Business Email",
                ["Plugins.Payments.Paytm.Fields.BusinessEmail.Hint"] = "Specify your Paytm business email.",
                ["Plugins.Payments.Paytm.Fields.PassProductNamesAndTotals"] = "Pass product names and order totals to Paytm",
                ["Plugins.Payments.Paytm.Fields.PassProductNamesAndTotals.Hint"] = "Check if product names and order totals should be passed to Paytm.",
                ["Plugins.Payments.Paytm.Fields.PDTToken"] = "PDT Identity Token",
                ["Plugins.Payments.Paytm.Fields.PDTToken.Hint"] = "Specify PDT identity token",
                ["Plugins.Payments.Paytm.Fields.RedirectionTip"] = "You will be redirected to Paytm site to complete the order.",
              //  ["Plugins.Payments.Paytm.Fields.UseSandbox"] = "Use Sandbox",
                //["Plugins.Payments.Paytm.Fields.UseSandbox.Hint"] = "Check to enable Sandbox (testing environment).",
            
                ["Plugins.Payments.Paytm.PaymentMethodDescription"] = "You will be redirected to Paytm site to complete the payment",
                ["Plugins.Payments.Paytm.RoundingWarning"] = "It looks like you have \"ShoppingCartSettings.RoundPricesDuringCalculation\" setting disabled. Keep in mind that this can lead to a discrepancy of the order total amount, as Paytm only rounds to two decimals.",

            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<PaytmPaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Paytm");

            await base.UninstallAsync();
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.Paytm.PaymentMethodDescription");
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

       #endregion
    }
}
