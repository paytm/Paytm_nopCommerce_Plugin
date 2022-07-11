using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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
using Paytm;

namespace Nop.Plugin.Payments.Paytm.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PaymentPaytmController : BasePaymentController
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
             PaytmPaymentSettings paytmPaymentSettings,
            ShoppingCartSettings shoppingCartSettings)
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
            _paytmPaymentSettings = paytmPaymentSettings;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var paytmPaymentSettings = await _settingService.LoadSettingAsync<PaytmPaymentSettings>(storeScope);

            ConfigurationModel model;

            string scheme = HttpContext.Request.Scheme;
            string host = HttpContext.Request.Host.ToString();

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
                    env = paytmPaymentSettings.env,
                    webhook = string.Concat(scheme, "://", host, "/Plugins/PaymentPaytm/Webhook"),
                    ActiveStoreScopeConfiguration = storeScope
                };
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
                    UseDefaultCallBack = paytmPaymentSettings.UseDefaultCallBack,
                    PaymentUrl = "https://securegw.paytm.in/order/process",
                    TxnStatusUrl = "https://securegw.paytm.in/order/status",
                    env = paytmPaymentSettings.env,
                    webhook = string.Concat(scheme, "://", host, "/Plugins/PaymentPaytm/Webhook"),
                    ActiveStoreScopeConfiguration = storeScope
                };
            }

            if (storeScope <= 0)
                return View("~/Plugins/Payments.Paytm/Views/Configure.cshtml", model);

            model.MerchantId_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.MerchantId, storeScope);
            model.MerchantKey_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.MerchantKey, storeScope);
            model.IndustryTypeId_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.IndustryTypeId, storeScope);
            model.PaymentUrl_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.PaymentUrl, storeScope);
            model.TxnStatusUrl_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.TxnStatusUrl, storeScope);
            model.UseDefaultCallBack_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.UseDefaultCallBack, storeScope);
            model.env_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.env, storeScope);
            model.PdtToken_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.PdtToken, storeScope);
            model.webhook_OverrideForStore = await _settingService.SettingExistsAsync(paytmPaymentSettings, x => x.webhook, storeScope);
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
            return _paytmPaymentSettings.TxnStatusUrl = url;
        }

        public ActionResult JSCheckoutView(string token, string orderid, string amount, string mid)
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
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var paytmPaymentSettings = await _settingService.LoadSettingAsync<PaytmPaymentSettings>(storeScope);

            //save settings
            paytmPaymentSettings.MerchantId = model.MerchantId;
            paytmPaymentSettings.MerchantKey = model.MerchantKey;
            paytmPaymentSettings.PdtToken = model.PdtToken;
            paytmPaymentSettings.env = model.env;
            paytmPaymentSettings.IndustryTypeId = model.IndustryTypeId;
            paytmPaymentSettings.Website = model.Website;
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
            paytmPaymentSettings.webhook = model.webhook;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.MerchantId, model.MerchantId_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.MerchantKey, model.MerchantKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.PdtToken, model.PdtToken_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.PaymentUrl, model.PaymentUrl_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.TxnStatusUrl, model.TxnStatusUrl_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.UseDefaultCallBack, model.UseDefaultCallBack_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.Website, model.Website_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.IndustryTypeId, model.IndustryTypeId_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.env, model.env_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.CallBackUrl, model.CallBackUrl_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(paytmPaymentSettings, x => x.webhook, model.webhook_OverrideForStore, storeScope, false);
            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();

        }

        public void Webhook()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            string paymentstatusnew = string.Empty;
            string orderId = string.Empty;
            string paytmchecksum = string.Empty;
            bool checkSumMatch = false;
            string workingKey = _paytmPaymentSettings.MerchantKey;
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

                orderId = Request.Form["ORDERID"];
                paymentstatusnew = Request.Form["STATUS"];
                if (parameters.ContainsKey("CHECKSUMHASH"))
                {
                    paytmchecksum = parameters["CHECKSUMHASH"];
                    parameters.Remove("CHECKSUMHASH");
                }
                if (!string.IsNullOrEmpty(paytmchecksum) && Checksum.verifySignature(parameters, workingKey, paytmchecksum))
                {
                    checkSumMatch = true;
                }
            }
            string querystring = HttpContext.Request.QueryString.ToString();
            var order = _orderService.GetOrderByCustomOrderNumberAsync(orderId).Result;
            string paymentstatus = order.PaymentStatus.ToString();
            if (checkSumMatch == true)
            {
                if (order != null)
                {
                    if (paymentstatus == "Pending" && paymentstatusnew == "TXN_SUCCESS")
                    {
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.OrderStatus = OrderStatus.Processing;
                        _orderService.UpdateOrderAsync(order);

                    }
                    if (paymentstatus == "Pending" && paymentstatusnew == "TXN_FAILURE")
                    {
                        order.PaymentStatus = PaymentStatus.Fail;
                        order.OrderStatus = OrderStatus.Cancelled;
                        _orderService.UpdateOrderAsync(order);
                    }
                }
            }

        }



        public ActionResult Return()
        {

            var processor = _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.Paytm").Result as PaytmPaymentProcessor;
            if (processor == null || !_paymentPluginManager.IsPluginActive(processor) || !processor.PluginDescriptor.Installed)
                throw new NopException("Paytm module cannot be loaded");


            var myUtility = new PaytmHelper();
            string orderId, amount, authDesc, resCode;
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


                if (!string.IsNullOrEmpty(paytmChecksum) && Checksum.verifySignature(parameters, workingKey, paytmChecksum))
                {
                    checkSumMatch = true;
                }
            }

            orderId = parameters["ORDERID"];
            amount = parameters["TXNAMOUNT"];
            resCode = parameters["RESPCODE"];
            authDesc = parameters["STATUS"];

            var order = _orderService.GetOrderByCustomOrderNumberAsync(orderId).Result;

            if (checkSumMatch == true)
            {

                if (resCode == "01" && authDesc == "TXN_SUCCESS")
                {
                    if (TxnStatus(orderId, order.OrderTotal.ToString("0.00")))
                    {
                        if (_orderProcessingService.CanMarkOrderAsPaid(order))
                        {
                            _orderProcessingService.MarkOrderAsPaidAsync(order);
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
                    _orderProcessingService.CancelOrderAsync(order, false);
                    order.OrderStatus = OrderStatus.Cancelled;
                    _orderService.UpdateOrderAsync(order);
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


            string checksum = Checksum.generateSignature(parameters, _paytmPaymentSettings.MerchantKey);//.Replace("+", "%2B");
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
        public async Task<IActionResult> RoundingWarning(bool passProductNamesAndTotals)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = await _localizationService.GetResourceAsync("Plugins.Payments.Paytm.RoundingWarning") });

            return Json(new { Result = string.Empty });
        }

        public async Task<IActionResult> PDTHandler()
        {
            var tx = _webHelper.QueryString<string>("tx");

            if (await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.Paytm") is not PaytmPaymentProcessor processor || !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("Paytm module cannot be loaded");

            var (result, values, response) = await processor.GetPdtDetailsAsync(tx);

            if (result)
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

                var order = await _orderService.GetOrderByGuidAsync(orderNumberGuid);

                if (order == null)
                    return RedirectToAction("Index", "Home", new { area = string.Empty });

                var mcGross = decimal.Zero;

                try
                {
                    mcGross = decimal.Parse(values["mc_gross"], new CultureInfo("en-US"));
                }
                catch (Exception exc)
                {
                    await _logger.ErrorAsync("Paytm PDT. Error getting mc_gross", exc);
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
                values.TryGetValue("mc_fee", out var mcFee);

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
                sb.AppendLine("mc_fee: " + mcFee);

                var newPaymentStatus = PaytmHelper.GetPaymentStatus(paymentStatus, string.Empty);
                sb.AppendLine("New payment status: " + newPaymentStatus);

                //order note
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = sb.ToString(),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                //validate order total
                var orderTotalSentToPaytm = await _genericAttributeService.GetAttributeAsync<decimal?>(order, PaytmHelper.OrderTotalSentToPaytm);
                if (orderTotalSentToPaytm.HasValue && mcGross != orderTotalSentToPaytm.Value)
                {
                    var errorStr = $"Paytm PDT. Returned order total {mcGross} doesn't equal order total {order.OrderTotal}. Order# {order.Id}.";
                    //log
                    await _logger.ErrorAsync(errorStr);
                    //order note
                    await _orderService.InsertOrderNoteAsync(new OrderNote
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
                    await _genericAttributeService.SaveAttributeAsync<decimal?>(order, PaytmHelper.OrderTotalSentToPaytm, null);

                if (newPaymentStatus != PaymentStatus.Paid)
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });

                if (!_orderProcessingService.CanMarkOrderAsPaid(order))
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });

                //mark order as paid
                order.AuthorizationTransactionId = txnId;
                await _orderService.UpdateOrderAsync(order);
                await _orderProcessingService.MarkOrderAsPaidAsync(order);

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

                var order = await _orderService.GetOrderByGuidAsync(orderNumberGuid);
                if (order == null)
                    return RedirectToAction("Index", "Home", new { area = string.Empty });

                //order note
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = "Paytm PDT failed. " + response,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
        }

        public async Task<IActionResult> CancelOrder()
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var customer = await _workContext.GetCurrentCustomerAsync();
            var order = (await _orderService.SearchOrdersAsync(store.Id,
                customerId: customer.Id, pageSize: 1)).FirstOrDefault();

            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("Homepage");
        }

        #endregion
    }
}
