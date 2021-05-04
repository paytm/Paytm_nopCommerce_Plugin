using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;
namespace Nop.Plugin.Payments.Paytm
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute("Plugin.Payments.Paytm.Return",
                 "Plugins/PaymentPaytm/Return",
                 new { controller = "PaymentPaytm", action = "Return" });

            /*routeBuilder.MapRoute("Plugin.Payments.Paytm.Return",
                "Plugins/PaymentPaytm/Return",
                new { controller = "PaymentPaytm", action = "Return" },
                new[] { "Nop.Plugin.Payments.Paytm.Controllers" });*/
        }

        public int Priority
        {
            get { return -1; }
        }
    }
}
