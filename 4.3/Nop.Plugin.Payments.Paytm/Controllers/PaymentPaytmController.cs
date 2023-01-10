using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Paytm.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Http;
using Paytm;
using System.Web;
using System.Text.RegularExpressions;

namespace Nop.Plugin.Payments.Paytm.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PaymentPaytmController:BasePaymentController
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly PaymentSettings _paymentSettings;
        private readonly IPaymentService _paymentService;
        private readonly PaytmPaymentSettings _paytmPaymentSettings;
        #endregion

        #region Ctor

        public PaymentPaytmController(IGenericAttributeService genericAttributeService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            ILogger logger,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWorkContext workContext,
            ShoppingCartSettings shoppingCartSettings,
            PaymentSettings paymentSettings,
            PaytmPaymentSettings paytmPaymentSettings,
            IPaymentService paymentService
            )
        {
            _genericAttributeService = genericAttributeService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _permissionService = permissionService;
            _localizationService = localizationService;
            _logger = logger;
            _notificationService = notificationService;
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
            _shoppingCartSettings = shoppingCartSettings;
            _paymentSettings = paymentSettings;
            _paytmPaymentSettings = paytmPaymentSettings;
            _paymentService = paymentService;
        }

        #endregion

        #region Utilities

        protected virtual void ProcessRecurringPayment(string invoiceId, PaymentStatus newPaymentStatus, string transactionId, string ipnInfo)
        {
            Guid orderNumberGuid;

            try
            {
                orderNumberGuid = new Guid(invoiceId);
            }
            catch
            {
                orderNumberGuid = Guid.Empty;
            }

            var order = _orderService.GetOrderByGuid(orderNumberGuid);
            if (order == null)
            {
                _logger.Error("Paytm IPN. Order is not found", new NopException(ipnInfo));
                return;
            }

            var recurringPayments = _orderService.SearchRecurringPayments(initialOrderId: order.Id);

            foreach (var rp in recurringPayments)
            {
                switch (newPaymentStatus)
                {
                    case PaymentStatus.Authorized:
                    case PaymentStatus.Paid:
                        {
                            var recurringPaymentHistory = _orderService.GetRecurringPaymentHistory(rp);
                            if (!recurringPaymentHistory.Any())
                            {
                                _orderService.InsertRecurringPaymentHistory(new RecurringPaymentHistory
                                {
                                    RecurringPaymentId = rp.Id,
                                    OrderId = order.Id,
                                    CreatedOnUtc = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                //next payments
                                var processPaymentResult = new ProcessPaymentResult
                                {
                                    NewPaymentStatus = newPaymentStatus
                                };
                                if (newPaymentStatus == PaymentStatus.Authorized)
                                    processPaymentResult.AuthorizationTransactionId = transactionId;
                                else
                                    processPaymentResult.CaptureTransactionId = transactionId;

                                _orderProcessingService.ProcessNextRecurringPayment(rp,
                                    processPaymentResult);
                            }
                        }

                        break;
                    case PaymentStatus.Voided:
                        //failed payment
                        var failedPaymentResult = new ProcessPaymentResult
                        {
                            Errors = new[] { $"Paytm IPN. Recurring payment is {nameof(PaymentStatus.Voided).ToLower()} ." },
                            RecurringPaymentFailed = true
                        };
                        _orderProcessingService.ProcessNextRecurringPayment(rp, failedPaymentResult);
                        break;
                }
            }
            _logger.Information("Paytm IPN. Recurring info", new NopException(ipnInfo));
        }

        protected virtual void ProcessPayment(string orderNumber, string ipnInfo, PaymentStatus newPaymentStatus, decimal mcGross, string transactionId)
        {
            Guid orderNumberGuid;

            try
            {
                orderNumberGuid = new Guid(orderNumber);
            }
            catch
            {
                orderNumberGuid = Guid.Empty;
            }

            var order = _orderService.GetOrderByGuid(orderNumberGuid);

            if (order == null)
            {
                _logger.Error("Paytm IPN. Order is not found", new NopException(ipnInfo));
                return;
            }

            //order note
            _orderService.InsertOrderNote(new OrderNote
            {
                OrderId = order.Id,
                Note = ipnInfo,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            //validate order total
            if ((newPaymentStatus == PaymentStatus.Authorized || newPaymentStatus == PaymentStatus.Paid) && !Math.Round(mcGross, 2).Equals(Math.Round(order.OrderTotal, 2)))
            {
                var errorStr = $"Paytm IPN. Returned order total {mcGross} doesn't equal order total {order.OrderTotal}. Order# {order.Id}.";
                //log
                _logger.Error(errorStr);
                //order note
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = errorStr,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return;
            }

            switch (newPaymentStatus)
            {
                case PaymentStatus.Authorized:
                    if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                        _orderProcessingService.MarkAsAuthorized(order);
                    break;
                case PaymentStatus.Paid:
                    if (_orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        order.AuthorizationTransactionId = transactionId;
                        _orderService.UpdateOrder(order);

                        _orderProcessingService.MarkOrderAsPaid(order);
                    }

                    break;
                case PaymentStatus.Refunded:
                    var totalToRefund = Math.Abs(mcGross);
                    if (totalToRefund > 0 && Math.Round(totalToRefund, 2).Equals(Math.Round(order.OrderTotal, 2)))
                    {
                        //refund
                        if (_orderProcessingService.CanRefundOffline(order))
                            _orderProcessingService.RefundOffline(order);
                    }
                    else
                    {
                        //partial refund
                        if (_orderProcessingService.CanPartiallyRefundOffline(order, totalToRefund))
                            _orderProcessingService.PartiallyRefundOffline(order, totalToRefund);
                    }

                    break;
                case PaymentStatus.Voided:
                    if (_orderProcessingService.CanVoidOffline(order))
                        _orderProcessingService.VoidOffline(order);

                    break;
            }
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var paytmPaymentSettings = _settingService.LoadSetting<PaytmPaymentSettings>(storeScope);
            ConfigurationModel model;
            if (paytmPaymentSettings.env == "Stage")
            {
                model = new ConfigurationModel
                {

                    MerchantId = paytmPaymentSettings.MerchantId,
                    MerchantKey = paytmPaymentSettings.MerchantKey,
                    Website = paytmPaymentSettings.Website,
                    IndustryTypeId = paytmPaymentSettings.IndustryTypeId,
                    CallBackUrl = paytmPaymentSettings.CallBackUrl,
                    UseDefaultCallBack = paytmPaymentSettings.UseDefaultCallBack,
                    PaymentUrl = "https://securegw-stage.paytm.in/order/process",
                    TxnStatusUrl = "https://securegw-stage.paytm.in/order/status",
                    ActiveStoreScopeConfiguration = storeScope,
                    env = paytmPaymentSettings.env

                };
               // return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);
            }
            else
            {
                 model = new ConfigurationModel
                {

                    MerchantId = paytmPaymentSettings.MerchantId,
                    MerchantKey = paytmPaymentSettings.MerchantKey,
                    Website = paytmPaymentSettings.Website,
                    IndustryTypeId = paytmPaymentSettings.IndustryTypeId,
                    CallBackUrl = paytmPaymentSettings.CallBackUrl,
                     PaymentUrl = "https://securegw.paytm.in/order/process",
                     TxnStatusUrl = "https://securegw.paytm.in/order/status",
                     UseDefaultCallBack = paytmPaymentSettings.UseDefaultCallBack,
                    ActiveStoreScopeConfiguration = storeScope,
                    env = paytmPaymentSettings.env

                };
              //  return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);
            }

          //  return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);

            if (storeScope <= 0)
                return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);
            model.MerchantId_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.MerchantId, storeScope);
            model.IndustryTypeId_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.IndustryTypeId, storeScope);
            model.MerchantKey_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.MerchantKey, storeScope);
            model.CallBackUrl_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.CallBackUrl, storeScope);
            model.PaymentUrl_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.PaymentUrl, storeScope);
            model.TxnStatusUrl_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.TxnStatusUrl, storeScope);
            model.UseDefaultCallBack_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.UseDefaultCallBack, storeScope);
            model.env_OverrideForStore = _settingService.SettingExists(paytmPaymentSettings, x => x.env, storeScope);
            return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);
        }
        private string GetStatusUrl()
        {
            string url = string.Empty;
            if (_paytmPaymentSettings.env == "Stage")
            {
                url = "https://securegw-stage.paytm.in/order/status";
            }
            else
            {
                url = "https://securegw.paytm.in/order/status";
            }
            return _paytmPaymentSettings.TxnStatusUrl=url;
        }
        public ActionResult JSCheckoutView(string token, string orderid, string amount,string mid) 
        {
           
            ViewData["env"] = _paytmPaymentSettings.env;
            ViewData["OrderId"] = Request.Cookies["orderid"];
            ViewData["txntoken"] = Request.Cookies["token"];
            ViewData["amount"] = Request.Cookies["amount"];
            ViewData["mid"] = Request.Cookies["mid"];
         
            String viewName = "~/Plugins/Payments.Paytm/Views/JSCheckoutView.cshtml";
            return View(viewName);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var paytmPaymentSettings = _settingService.LoadSetting<PaytmPaymentSettings>(storeScope);
            paytmPaymentSettings.env = model.env;
            paytmPaymentSettings.MerchantId = model.MerchantId;
            paytmPaymentSettings.MerchantKey = model.MerchantKey;
            paytmPaymentSettings.Website = model.Website;
            paytmPaymentSettings.IndustryTypeId = model.IndustryTypeId;
            if (model.env == "Stage")
            {
                paytmPaymentSettings.PaymentUrl = "https://securegw-stage.paytm.in/order/process";
                paytmPaymentSettings.TxnStatusUrl = "https://securegw-stage.paytm.in/order/status";
            }
            if (model.env == "Prod")
            {
                paytmPaymentSettings.PaymentUrl = "https://securegw.paytm.in/order/process";
                paytmPaymentSettings.TxnStatusUrl = "https://securegw.paytm.in/order/status";
            }
            paytmPaymentSettings.PaymentUrl = model.PaymentUrl;
            paytmPaymentSettings.CallBackUrl = model.CallBackUrl;
            paytmPaymentSettings.TxnStatusUrl = model.TxnStatusUrl;
            paytmPaymentSettings.UseDefaultCallBack = model.UseDefaultCallBack;
            _settingService.SaveSetting(paytmPaymentSettings);
            
            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(paytmPaymentSettings, x => x.MerchantId, model.MerchantId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paytmPaymentSettings, x => x.MerchantKey, model.MerchantKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paytmPaymentSettings, x => x.Website, model.Website_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paytmPaymentSettings, x => x.IndustryTypeId, model.IndustryTypeId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paytmPaymentSettings, x => x.PaymentUrl, model.PaymentUrl_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paytmPaymentSettings, x => x.CallBackUrl, model.CallBackUrl_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paytmPaymentSettings, x => x.TxnStatusUrl, model.TxnStatusUrl_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [IgnoreAntiforgeryToken]
        public ActionResult Return()
        { 
            var processor = _paymentPluginManager.LoadPluginBySystemName("Payments.Paytm") as PaytmPaymentProcessor;
            if (processor == null || !_paymentPluginManager.IsPluginActive(processor) || !processor.PluginDescriptor.Installed)
                throw new NopException("Paytm module cannot be loaded");


            var myUtility = new PaytmHelper();
            string orderId, amount, authDesc, resCode, orderIdRaw;
            bool checkSumMatch = false;
            //Assign following values to send it to verifychecksum function.
            if (String.IsNullOrWhiteSpace(_paytmPaymentSettings.MerchantKey))
                throw new NopException("Paytm key is not set");

            string workingKey = _paytmPaymentSettings.MerchantKey;
            string paytmChecksum = null;

            Dictionary<string, string> parameters = new Dictionary<string, string>();
           
            if (Request.Form.Keys.Count > 0)
            {

                foreach (string key in Request.Form.Keys)
                {
                    if (Request.Form[key].Contains("|"))
                    {
                        parameters.Add(key.Trim(), "");
                    }
                    else
                    {
                        parameters.Add(key.Trim(), Request.Form[key]);
                    }
                }

                if (parameters.ContainsKey("CHECKSUMHASH"))
                {
                    paytmChecksum = parameters["CHECKSUMHASH"];
                    parameters.Remove("CHECKSUMHASH");
                }

                //paytm does not exist
                //if (!string.IsNullOrEmpty(paytmChecksum) && paytm.CheckSum.verifyCheckSum(workingKey, parameters, paytmChecksum))
                //{
                //    checkSumMatch = true;
                //}
                if (!string.IsNullOrEmpty(paytmChecksum) && Checksum.verifySignature(parameters, workingKey, paytmChecksum))
                {
                    checkSumMatch = true;
                }
            }

            orderIdRaw = parameters["ORDERID"];
            orderId = Regex.Replace(orderIdRaw, @"_.*", "");
            amount = parameters["TXNAMOUNT"];
            resCode = parameters["RESPCODE"];
            authDesc = parameters["STATUS"];

            var order = _orderService.GetOrderById(Convert.ToInt32(orderId));
            if (checkSumMatch == true)
            {
                if (resCode == "01" && authDesc == "TXN_SUCCESS")
                {
                    if (TxnStatus(orderIdRaw, order.OrderTotal.ToString("0.00")))
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
                else if (authDesc == "TXN_FAILURE")
                {
                    _orderProcessingService.CancelOrder(order, false);
                    order.OrderStatus = OrderStatus.Cancelled;
                    _orderService.UpdateOrder(order);
                    return RedirectToRoute("OrderDetails", new { orderId = order.Id });
                }
                else
                {
                    return Content("Security Error. Illegal access detected");
                }
            }
            else if (string.IsNullOrEmpty(paytmChecksum))
            {
                return Content("Please Contact Customer Care");
            }
            else
            {
                return Content("Security Error. Illegal access detected, Checksum failed");
            }
        }

        private bool TxnStatus(string orderId, String amount)
        {

            String uri = GetStatusUrl();
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("MID", _paytmPaymentSettings.MerchantId);
            parameters.Add("ORDERID", orderId);

         //   string checksum = paytm.CheckSum.generateCheckSum(_paytmPaymentSettings.MerchantKey, parameters);//.Replace("+", "%2B");
            string checksum = Checksum.generateSignature(parameters,_paytmPaymentSettings.MerchantKey);//.Replace("+", "%2B");
            try
            {
                string postData = "{\"MID\":\"" + _paytmPaymentSettings.MerchantId + "\",\"ORDERID\":\"" + orderId + "\",\"CHECKSUMHASH\":\"" + System.Net.WebUtility.UrlEncode(checksum) + "\"}";
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

            }
            return false;
        }


        //action displaying notification (warning) to a store owner about inaccurate Paytm rounding
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult RoundingWarning(bool passProductNamesAndTotals)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = _localizationService.GetResource("Plugins.Payments.Paytm.RoundingWarning") });

            return Json(new { Result = string.Empty });
        }

        public IActionResult PDTHandler()
        {
            var tx = _webHelper.QueryString<string>("tx");

            if (!(_paymentPluginManager.LoadPluginBySystemName("Payments.Paytm") is PaytmPaymentProcessor processor) || !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("Paytm module cannot be loaded");

            if (processor.GetPdtDetails(tx, out var values, out var response))
            {
                values.TryGetValue("custom", out var orderNumber);
                var orderNumberGuid = Guid.Empty;
                try
                {
                    orderNumberGuid = new Guid(orderNumber);
                }
                catch
                {
                    // ignored
                }

                var order = _orderService.GetOrderByGuid(orderNumberGuid);

                if (order == null)
                    return RedirectToAction("Index", "Home", new { area = string.Empty });

                var mcGross = decimal.Zero;

                try
                {
                    mcGross = decimal.Parse(values["mc_gross"], new CultureInfo("en-US"));
                }
                catch (Exception exc)
                {
                    _logger.Error("Paytm PDT. Error getting mc_gross", exc);
                }

                values.TryGetValue("payer_status", out var payerStatus);
                values.TryGetValue("payment_status", out var paymentStatus);
                values.TryGetValue("pending_reason", out var pendingReason);
                values.TryGetValue("mc_currency", out var mcCurrency);
                values.TryGetValue("txn_id", out var txnId);
                values.TryGetValue("payment_type", out var paymentType);
                values.TryGetValue("payer_id", out var payerId);
                values.TryGetValue("receiver_id", out var receiverId);
                values.TryGetValue("invoice", out var invoice);
                values.TryGetValue("payment_fee", out var paymentFee);

                var sb = new StringBuilder();
                sb.AppendLine("Paytm PDT:");
                sb.AppendLine("mc_gross: " + mcGross);
                sb.AppendLine("Payer status: " + payerStatus);
                sb.AppendLine("Payment status: " + paymentStatus);
                sb.AppendLine("Pending reason: " + pendingReason);
                sb.AppendLine("mc_currency: " + mcCurrency);
                sb.AppendLine("txn_id: " + txnId);
                sb.AppendLine("payment_type: " + paymentType);
                sb.AppendLine("payer_id: " + payerId);
                sb.AppendLine("receiver_id: " + receiverId);
                sb.AppendLine("invoice: " + invoice);
                sb.AppendLine("payment_fee: " + paymentFee);

                var newPaymentStatus = PaytmHelper.GetPaymentStatus(paymentStatus, string.Empty);
                sb.AppendLine("New payment status: " + newPaymentStatus);

                //order note
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = sb.ToString(),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                //validate order total
                var orderTotalSentToPaytm = _genericAttributeService.GetAttribute<decimal?>(order, PaytmHelper.OrderTotalSentToPaytm);
                if (orderTotalSentToPaytm.HasValue && mcGross != orderTotalSentToPaytm.Value)
                {
                    var errorStr = $"Paytm PDT. Returned order total {mcGross} doesn't equal order total {order.OrderTotal}. Order# {order.Id}.";
                    //log
                    _logger.Error(errorStr);
                    //order note
                    _orderService.InsertOrderNote(new OrderNote
                    {
                        OrderId = order.Id,
                        Note = errorStr,
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });

                    return RedirectToAction("Index", "Home", new { area = string.Empty });
                }

                //clear attribute
                if (orderTotalSentToPaytm.HasValue)
                    _genericAttributeService.SaveAttribute<decimal?>(order, PaytmHelper.OrderTotalSentToPaytm, null);

                if (newPaymentStatus != PaymentStatus.Paid)
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });

                if (!_orderProcessingService.CanMarkOrderAsPaid(order))
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });

                //mark order as paid
                order.AuthorizationTransactionId = txnId;
                _orderService.UpdateOrder(order);
                _orderProcessingService.MarkOrderAsPaid(order);

                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            else
            {
                if (!values.TryGetValue("custom", out var orderNumber))
                    orderNumber = _webHelper.QueryString<string>("cm");

                var orderNumberGuid = Guid.Empty;

                try
                {
                    orderNumberGuid = new Guid(orderNumber);
                }
                catch
                {
                    // ignored
                }

                var order = _orderService.GetOrderByGuid(orderNumberGuid);
                if (order == null)
                    return RedirectToAction("Index", "Home", new { area = string.Empty });

                //order note
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = "Paytm PDT failed. " + response,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
        }

        public IActionResult IPNHandler()
        {
            byte[] parameters;

            using (var stream = new MemoryStream())
            {
                Request.Body.CopyTo(stream);
                parameters = stream.ToArray();
            }

            var strRequest = Encoding.ASCII.GetString(parameters);

            if (!(_paymentPluginManager.LoadPluginBySystemName("Payments.Paytm") is PaytmPaymentProcessor processor) || !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("Paytm module cannot be loaded");

            if (!processor.VerifyIpn(strRequest, out var values))
            {
                _logger.Error("Paytm IPN failed.", new NopException(strRequest));

                //nothing should be rendered to visitor
                return Content(string.Empty);
            }

            var mcGross = decimal.Zero;

            try
            {
                mcGross = decimal.Parse(values["mc_gross"], new CultureInfo("en-US"));
            }
            catch
            {
                // ignored
            }

            values.TryGetValue("payment_status", out var paymentStatus);
            values.TryGetValue("pending_reason", out var pendingReason);
            values.TryGetValue("txn_id", out var txnId);
            values.TryGetValue("txn_type", out var txnType);
            values.TryGetValue("rp_invoice_id", out var rpInvoiceId);

            var sb = new StringBuilder();
            sb.AppendLine("Paytm IPN:");
            foreach (var kvp in values)
            {
                sb.AppendLine(kvp.Key + ": " + kvp.Value);
            }

            var newPaymentStatus = PaytmHelper.GetPaymentStatus(paymentStatus, pendingReason);
            sb.AppendLine("New payment status: " + newPaymentStatus);

            var ipnInfo = sb.ToString();

            switch (txnType)
            {
                case "recurring_payment":
                    ProcessRecurringPayment(rpInvoiceId, newPaymentStatus, txnId, ipnInfo);
                    break;
                case "recurring_payment_failed":
                    if (Guid.TryParse(rpInvoiceId, out var orderGuid))
                    {
                        var order = _orderService.GetOrderByGuid(orderGuid);
                        if (order != null)
                        {
                            var recurringPayment = _orderService.SearchRecurringPayments(initialOrderId: order.Id)
                                .FirstOrDefault();
                            //failed payment
                            if (recurringPayment != null)
                                _orderProcessingService.ProcessNextRecurringPayment(recurringPayment,
                                    new ProcessPaymentResult
                                    {
                                        Errors = new[] { txnType },
                                        RecurringPaymentFailed = true
                                    });
                        }
                    }

                    break;
                default:
                    values.TryGetValue("custom", out var orderNumber);
                    ProcessPayment(orderNumber, ipnInfo, newPaymentStatus, mcGross, txnId);

                    break;
            }

            //nothing should be rendered to visitor
            return Content(string.Empty);
        }

        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(_storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();

            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("Homepage");
        }

        #endregion


    }
}
