using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.Paytm.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Paytm.Components
{
    [ViewComponent(Name = "PaymentPaytm")]
    public class PaymentPaytmViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var model = new PaymentInfoModel()
            {
                
            };

            return View("~/Plugins/Payments.Paytm/Views/PaymentInfo.cshtml", model);
        }
    }
}
