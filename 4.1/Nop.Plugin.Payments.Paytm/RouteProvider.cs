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
           routeBuilder.MapRoute("Plugin.Payments.Paytm.JSCheckoutView", "Plugins/PaymentPaytm/JSCheckoutView",
          new { controller = "PaymentPaytm", action = "JSCheckoutView" });
        }

        public int Priority
        {
            get { return -1; }
        }
    }
}
