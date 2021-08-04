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
        /// <param name="endpointRouteBuilder">Route builder</param>
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //PDT
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Paytm.PDTHandler", "Plugins/PaymentPaytm/PDTHandler",
                 new { controller = "PaymentPaytm", action = "PDTHandler" });

            //IPN
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Paytm.IPNHandler", "Plugins/PaymentPaytm/IPNHandler",
                 new { controller = "PaymentPaytmIpn", action = "IPNHandler" });

            //Cancel
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Paytm.CancelOrder", "Plugins/PaymentPaytm/CancelOrder",
                 new { controller = "PaymentPaytm", action = "CancelOrder" });
            // Response
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Paytm.Return", "Plugins/PaymentPaytm/Return",
               new { controller = "PaymentPaytm", action = "Return" });

            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Paytm.JSCheckoutView", "Plugins/PaymentPaytm/JSCheckoutView",
              new { controller = "PaymentPaytm", action = "JSCheckoutView" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => -1;
    }
}
