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
using Paytm;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Web;
using Nop.Services.Localization;

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
        private readonly ILocalizationService _localizationService;


        public PaymentPaytmController(ISettingService settingService,
            IPaymentService paymentService, IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            PaytmPaymentSettings PaytmPaymentSettings,
            PaymentSettings paymentSettings, ILocalizationService localizationService)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._PaytmPaymentSettings = PaytmPaymentSettings;
            this._paymentSettings = paymentSettings;
            this._localizationService = localizationService;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            ConfigurationModel model;
            if (_PaytmPaymentSettings.env == "Stage")
            {
                model = new ConfigurationModel
                {

                    MerchantId = _PaytmPaymentSettings.MerchantId,
                    MerchantKey = _PaytmPaymentSettings.MerchantKey,
                    Website = _PaytmPaymentSettings.Website,
                    IndustryTypeId = _PaytmPaymentSettings.IndustryTypeId,
                    CallBackUrl = _PaytmPaymentSettings.CallBackUrl,
                   
                    PaymentUrl = "https://securegw-stage.paytm.in/order/process",
                  
                    env = _PaytmPaymentSettings.env

                };
                // return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);
            }
            else
            {
                model = new ConfigurationModel
                {

                    MerchantId = _PaytmPaymentSettings.MerchantId,
                    MerchantKey = _PaytmPaymentSettings.MerchantKey,
                    Website = _PaytmPaymentSettings.Website,
                    IndustryTypeId = _PaytmPaymentSettings.IndustryTypeId,
                    CallBackUrl = _PaytmPaymentSettings.CallBackUrl,
                    PaymentUrl = "https://securegw.paytm.in/order/process",
                  
                    env = _PaytmPaymentSettings.env

                };
                //  return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);
            }
   //         var model = new ConfigurationModel();
   //         model.MerchantId = _PaytmPaymentSettings.MerchantId;
			//model.MerchantKey = _PaytmPaymentSettings.MerchantKey;
			//model.Website = _PaytmPaymentSettings.Website;
			//model.IndustryTypeId = _PaytmPaymentSettings.IndustryTypeId;
           
   //         model.PaymentUrl = _PaytmPaymentSettings.PaymentUrl;
			//model.CallBackUrl = _PaytmPaymentSettings.CallBackUrl;
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
            _PaytmPaymentSettings.env = model.env;
            if (model.env == "Stage")
            {
                _PaytmPaymentSettings.PaymentUrl = "https://securegw-stage.paytm.in/order/process";

            }
            if (model.env == "Prod")
            {
                _PaytmPaymentSettings.PaymentUrl = "https://securegw.paytm.in/order/process";

            }
          //  _PaytmPaymentSettings.PaymentUrl = model.PaymentUrl;
            _PaytmPaymentSettings.CallBackUrl = model.CallBackUrl;
            _settingService.SaveSetting(_PaytmPaymentSettings);
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            return Configure();
        }

        public ActionResult JSCheckoutView(string token, string orderid, string amount, string mid)
        {

            ViewData["env"] = _PaytmPaymentSettings.env;
            ViewData["OrderId"] = Request.Cookies["orderid"].Value;
            ViewData["txntoken"] = Request.Cookies["token"].Value;
            ViewData["amount"] = Request.Cookies["amount"].Value;
            ViewData["mid"] = Request.Cookies["mid"].Value;

            String viewName = "~/Plugins/Payments.Paytm/Views/PaymentPaytm/JSCheckoutView.cshtml";
            return View(viewName);
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
            string paytmChecksum = null;

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            if (Request.Form.AllKeys.Length > 0)
            {
                
                foreach (string key in Request.Form.Keys)
                {
                    if (Request.Form[key].Contains("|"))
                    {
                        parameters.Add(key.Trim(), "");
                    }
                    else
                    {
                        parameters.Add(key.Trim(), Request.Form[key].Trim());
                    }
                }

                if (parameters.ContainsKey("CHECKSUMHASH"))
                {
                    paytmChecksum = parameters["CHECKSUMHASH"];
                    parameters.Remove("CHECKSUMHASH");
                }
                //if (!string.IsNullOrEmpty(paytmChecksum) && CheckSum.verifyCheckSum(workingKey, parameters, paytmChecksum))
                //{
                //    checkSumMatch = true;
                //}
                if (!string.IsNullOrEmpty(paytmChecksum) && Checksum.verifySignature(parameters,workingKey,paytmChecksum))
                {
                    checkSumMatch = true;
                }
                /*
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

                }*/
            }

            orderId = parameters["ORDERID"];
			Amount = parameters["TXNAMOUNT"];
			ResCode = parameters["RESPCODE"];
			AuthDesc = parameters["STATUS"];

            if (checkSumMatch == true)
            {
                var order = _orderService.GetOrderById(Convert.ToInt32(orderId));
                
                if (ResCode == "01" && AuthDesc == "TXN_SUCCESS")
                {
                    
                    if (TxnStatus(orderId, order.OrderTotal.ToString("0.00")))
                    {
                        if (_orderProcessingService.CanMarkOrderAsPaid(order))
                        {
                            _orderProcessingService.MarkOrderAsPaid(order);
                        }
                        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                    }
                    else
                    {
                        return Content("Amount Mismatch");
                    }
                }
                else if (AuthDesc == "TXN_FAILURE")
                {
                    var p = new PayModel();
                    p.RespMsg = parameters["RESPMSG"].ToString();
                    return View("~/Plugins/Payments.Paytm/Views/PaymentPaytm/Pay.cshtml", p);
                    //Response.Write("<script> $(document).ready(function(){  $(\"#submitButton\").on(\"click\",function() {alert('"+parameters["RESPMSG"].ToString()+"');});});<script>");
                    //alert('@TempData["alertMessage"]');
                    //return  Response.Write(parameters["RESPMSG"].ToString() + "< /br>" + "<a href=\"/Home/Index\">Click here to continue..</a>");
                    //return red
                    //return RedirectToRoute("ShoppingCart");
                    //return Content(parameters["RESPMSG"].ToString() + " < /br>" + "Html.ActionLink(\"Click here to continue..\",\"Index\", \"Home\")");
                    //return RedirectToAction("Index", "Home", new { area = "" });
                }
                else
                {
                    return Content("Security Error. Illegal access detected");
                }
            }
            else if (string.IsNullOrEmpty(paytmChecksum))
            {
                var p = new PayModel();
                p.RespMsg = parameters["RESPMSG"].ToString();
                return View("~/Plugins/Payments.Paytm/Views/PaymentPaytm/Pay.cshtml", p);
            }
            else
            {
                return Content("Security Error. Illegal access detected, Checksum failed");
            }
        }

        /*[ValidateInput(false)]
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

            if (checkSumMatch == true)
            {
                if (ResCode == "01" && AuthDesc == "TXN_SUCCESS")
                {
                    var order = _orderService.GetOrderById(Convert.ToInt32(orderId));
                    if (_orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        _orderProcessingService.MarkOrderAsPaid(order);
                    }
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                }
                else if (AuthDesc == "TXN_FAILURE")
                {
                    return RedirectToRoute("ShoppingCart");
                }
                else
                {
                    return Content("Security Error. Illegal access detected");
                }
            }
            else
            {
                return Content("Security Error. Illegal access detected, Checksum failed");
            }
        }*/

        private bool TxnStatus(string OrderId, String amount)
        {
            String uri = "https://pguat.paytm.com/oltp/HANDLER_INTERNAL/getTxnStatus";
            if (_PaytmPaymentSettings.PaymentUrl.ToLower().Contains("securegw.paytm.in"))
            {
                uri = "https://secure.paytm.in/oltp/HANDLER_INTERNAL/getTxnStatus";
            }

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("MID", _PaytmPaymentSettings.MerchantId);
            parameters.Add("ORDERID", OrderId);

           // string checksum = CheckSum.generateCheckSum(_PaytmPaymentSettings.MerchantKey, parameters);//.Replace("+", "%2B");
            string checksum = Checksum.generateSignature(parameters,_PaytmPaymentSettings.MerchantKey);//.Replace("+", "%2B");
            try
            {
                string postData = "{\"MID\":\""+ _PaytmPaymentSettings.MerchantId+ "\",\"ORDERID\":\""+OrderId+"\",\"CHECKSUMHASH\":\""+ HttpUtility.UrlEncode(checksum) + "\"}";
                
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
                webRequest.Method = "POST";
                webRequest.Accept = "application/json";
                webRequest.ContentType = "application/json";

                using (StreamWriter requestWriter2 = new StreamWriter(webRequest.GetRequestStream()))
                {
                    requestWriter2.Write("JsonData=" + postData);
                }
                string responseData = string.Empty;
                using (StreamReader responseReader = new StreamReader(webRequest.GetResponse().GetResponseStream()))
                {
                    responseData = responseReader.ReadToEnd();
                    if (responseData.Contains("TXN_SUCCESS") && responseData.Contains(amount))
                    {
                        return true;
                    }
                    else
                    {
                        //
                    }
                }
            }
            catch (Exception ex)
            {
                Response.Write("Error " + ex.Message);
            }
            return false;
        }
    }
}