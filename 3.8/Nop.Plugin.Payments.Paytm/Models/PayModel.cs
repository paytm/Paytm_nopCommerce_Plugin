using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Paytm.Models
{
    public class PayModel : BaseNopModel
    {
        public string RespCode { get; internal set; }
        public string RespMsg { get; internal set; }
        public string OrderId { get; internal set; }
    }
}
