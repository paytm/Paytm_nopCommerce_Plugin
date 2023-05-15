using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Paytm.Components
{
    [ViewComponent(Name = "PaymentPaytm")]
    public class PaymentPaytmViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Paytm/Views/PaymentInfo.cshtml");
        }
    }
}
