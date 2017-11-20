using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Mvc.Models;

namespace Nop.Plugin.Payments.Paytm.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.UseDefaultCallBack")]
        public bool UseDefaultCallBack { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.MerchantId")]
        public string MerchantId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.MerchantKey")] //Encryption Key
        public string MerchantKey { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.Website")]
        public string Website { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.IndustryTypeId")]//Payment URI
        public string IndustryTypeId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.PaymentUrl")]
        public string PaymentUrl { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.CallBackUrl")]
        public string CallBackUrl { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.TxnStatusUrl")]
        public string TxnStatusUrl { get; set; }

    }
}