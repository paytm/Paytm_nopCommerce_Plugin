﻿using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Paytm.Components;

public class PaymentPaytmViewComponent : NopViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View("~/Plugins/Payments.Paytm/Views/PaymentInfo.cshtml");
    }
}