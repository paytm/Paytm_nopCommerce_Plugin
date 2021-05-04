using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Paytm.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //PDT
            routeBuilder.MapRoute("Plugin.Payments.Paytm.PDTHandler", "Plugins/PaymentPaytm/PDTHandler",
                 new { controller = "PaymentPaytm", action = "PDTHandler" });

            //IPN
            routeBuilder.MapRoute("Plugin.Payments.Paytm.IPNHandler", "Plugins/PaymentPaytm/IPNHandler",
                 new { controller = "PaymentPaytm", action = "IPNHandler" });

            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.Paytm.CancelOrder", "Plugins/PaymentPaytm/CancelOrder",
                 new { controller = "PaymentPaytm", action = "CancelOrder" });
            // Response
            routeBuilder.MapRoute("Plugin.Payments.Paytm.Return",
               "Plugins/PaymentPaytm/Return",
               new { controller = "PaymentPaytm", action = "Return" });
            routeBuilder.MapRoute("Plugin.Payments.Paytm.JSCheckoutView", "Plugins/PaymentPaytm/JSCheckoutView",
           new { controller = "PaymentPaytm", action = "JSCheckoutView" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => -1;
    }
}