using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.Paytm
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //Return
            routes.MapRoute("Plugin.Payments.Paytm.Return",
                 "Plugins/PaymentPaytm/Return",
                 new { controller = "PaymentPaytm", action = "Return" },
                 new[] { "Nop.Plugin.Payments.Paytm.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.Paytm.JSCheckoutView",
                "Plugins/PaymentPaytm/JSCheckoutView",
                new { controller = "PaymentPaytm", action = "JSCheckoutView" },
                new[] { "Nop.Plugin.Payments.Paytm.Controllers" }
           );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
