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
using paytm;
//using CCA.Util;
using System.Collections.Specialized;
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
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        //CCACrypto ccaCrypto = new CCACrypto();
        #endregion

        #region Ctor

        public PaytmPaymentProcessor(PaytmPaymentSettings PaytmPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper)
        {
            this._PaytmPaymentSettings = PaytmPaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
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
            
            remotePostHelper.FormName = "PaytmForm";
			remotePostHelper.Url = _PaytmPaymentSettings.PaymentUrl;
            remotePostHelperData.Add("MID", _PaytmPaymentSettings.MerchantId.ToString());
			remotePostHelperData.Add("WEBSITE", _PaytmPaymentSettings.Website.ToString());
			remotePostHelperData.Add("CHANNEL_ID", "WEB");
			remotePostHelperData.Add("INDUSTRY_TYPE_ID", _PaytmPaymentSettings.IndustryTypeId.ToString());
			remotePostHelperData.Add("TXN_AMOUNT", postProcessPaymentRequest.Order.OrderTotal.ToString ("#.##"));
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
			   remotePostHelper.Add(item.Key,item.Value);
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


        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPaymentOld(PostProcessPaymentRequest postProcessPaymentRequest)
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

        #endregion
    }
}
