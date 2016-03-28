using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Paytm.Models;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;
using paytm;
using System.Collections.Specialized;

namespace Nop.Plugin.Payments.Paytm.Controllers
{
    public class PaymentPaytmController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly PaytmPaymentSettings _PaytmPaymentSettings;
        private readonly PaymentSettings _paymentSettings;

        public PaymentPaytmController(ISettingService settingService,
            IPaymentService paymentService, IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            PaytmPaymentSettings PaytmPaymentSettings,
            PaymentSettings paymentSettings)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._PaytmPaymentSettings = PaytmPaymentSettings;
            this._paymentSettings = paymentSettings;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new ConfigurationModel();
            model.MerchantId = _PaytmPaymentSettings.MerchantId;
			model.MerchantKey = _PaytmPaymentSettings.MerchantKey;
			model.Website = _PaytmPaymentSettings.Website;
			model.IndustryTypeId = _PaytmPaymentSettings.IndustryTypeId;
			model.PaymentUrl = _PaytmPaymentSettings.PaymentUrl;
			model.CallBackUrl = _PaytmPaymentSettings.CallBackUrl;
            return View("~/Plugins/Payments.Paytm/Views/PaymentPaytm/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _PaytmPaymentSettings.MerchantId = model.MerchantId;
			_PaytmPaymentSettings.MerchantKey = model.MerchantKey;
			_PaytmPaymentSettings.Website = model.Website;
			_PaytmPaymentSettings.IndustryTypeId = model.IndustryTypeId;
			_PaytmPaymentSettings.PaymentUrl = model.PaymentUrl;
			_PaytmPaymentSettings.CallBackUrl = model.CallBackUrl;
            _settingService.SaveSetting(_PaytmPaymentSettings);

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.Paytm/Views/PaymentPaytm/PaymentInfo.cshtml", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [ValidateInput(false)]
        public ActionResult Return(FormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Paytm") as PaytmPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("Paytm module cannot be loaded");


            var myUtility = new PaytmHelper();
			string orderId,  Amount, AuthDesc, ResCode;
			bool checkSumMatch = false;
            //Assign following values to send it to verifychecksum function.
			if (String.IsNullOrWhiteSpace(_PaytmPaymentSettings.MerchantKey))
                throw new NopException("Paytm key is not set");

			string workingKey = _PaytmPaymentSettings.MerchantKey;
        

			Dictionary<string, string> parameters = new Dictionary<string, string>();
			if (Request.Form.AllKeys.Length > 0)
			{

				string paytmChecksum="";
				foreach (string key in Request.Form.Keys){
					parameters.Add(key.Trim(), Request.Form[key].Trim());
				}

				if(parameters.ContainsKey("CHECKSUMHASH")){
					paytmChecksum = parameters["CHECKSUMHASH"];
					parameters.Remove("CHECKSUMHASH");
				}

				if (CheckSum.verifyCheckSum(workingKey, parameters, paytmChecksum))	{
					checkSumMatch = true;

				}

			}

			orderId = parameters["ORDERID"];
			Amount = parameters["TXNAMOUNT"];
			ResCode = parameters["RESPCODE"];
			AuthDesc = parameters["STATUS"];

			if (ResCode == "01") {
				if (checkSumMatch == true) {
					if (AuthDesc == "TXN_SUCCESS") {
						var order = _orderService.GetOrderById (Convert.ToInt32 (orderId));
						if (_orderProcessingService.CanMarkOrderAsPaid (order)) {
							_orderProcessingService.MarkOrderAsPaid (order);
						}
						return RedirectToRoute ("CheckoutCompleted", new { orderId = order.Id });
					} else if (AuthDesc == "TXN_FAILURE") {
						return Content ("Thank you for shopping with us. However, the transaction has been declined");

					} else {
						return Content ("Security Error. Illegal access detected");
					}
				} else {
					return Content ("Security Error. Illegal access detected, Checksum failed");
				}
			} else {
				return Content("Transaction has been Failed");
			}
        }

        [ValidateInput(false)]
        public ActionResult ReturnOLD(FormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Paytm") as PaytmPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("Paytm module cannot be loaded");


            var myUtility = new PaytmHelper();
			string orderId,  Amount, AuthDesc, ResCode;
			bool checkSumMatch = false;
			//Assign following values to send it to verifychecksum function.
			if (String.IsNullOrWhiteSpace(_PaytmPaymentSettings.MerchantKey))
				throw new NopException("Paytm key is not set");

			string workingKey = _PaytmPaymentSettings.MerchantKey;


			Dictionary<string, string> parameters = new Dictionary<string, string>();
			if (Request.Form.AllKeys.Length > 0)
			{

				string paytmChecksum="";
				foreach (string key in Request.Form.Keys){
					parameters.Add(key.Trim(), Request.Form[key].Trim());
				}

				if(parameters.ContainsKey("CHECKSUMHASH")){
					paytmChecksum = parameters["CHECKSUMHASH"];
					parameters.Remove("CHECKSUMHASH");
				}

				if (CheckSum.verifyCheckSum(workingKey, parameters, paytmChecksum))	{
					checkSumMatch = true;

				}

			}

			orderId = parameters["ORDERID"];
			Amount = parameters["TXNAMOUNT"];
			ResCode = parameters["RESPCODE"];
			AuthDesc = parameters["STATUS"];

			if (ResCode == "01") {
				if (checkSumMatch == true) {
					if (AuthDesc == "TXN_SUCCESS") {
						var order = _orderService.GetOrderById (Convert.ToInt32 (orderId));
						if (_orderProcessingService.CanMarkOrderAsPaid (order)) {
							_orderProcessingService.MarkOrderAsPaid (order);
						}
						return RedirectToRoute ("CheckoutCompleted", new { orderId = order.Id });
					} else if (AuthDesc == "TXN_FAILURE") {
						return Content ("Thank you for shopping with us. However, the transaction has been declined");

					} else {
						return Content ("Security Error. Illegal access detected");
					}
				} else {
					return Content ("Security Error. Illegal access detected, Checksum failed");
				}
			} else {
				return Content("Transaction has been Failed");
			}
        }
    }
}