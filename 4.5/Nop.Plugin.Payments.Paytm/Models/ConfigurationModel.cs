using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.Paytm.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.UseDefaultCallBack")]
        public bool UseDefaultCallBack { get; set; }
        public bool UseDefaultCallBack_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.MerchantId")]

        public string MerchantId { get; set; }
        public bool MerchantId_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.MerchantKey")] //Encryption Key
        public string MerchantKey { get; set; }
        public bool MerchantKey_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.Website")]
        public string Website { get; set; }
        public bool Website_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.IndustryTypeId")]//Payment URI
        public string IndustryTypeId { get; set; }
        public bool IndustryTypeId_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.PaymentUrl")]
        public string PaymentUrl { get; set; }
        public bool PaymentUrl_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.CallBackUrl")]
        public string CallBackUrl { get; set; }
        public bool CallBackUrl_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.TxnStatusUrl")]
        public string TxnStatusUrl { get; set; }
        public bool TxnStatusUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.env")]
        public string env { get; set; }
        public bool env_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.PdtToken")]
        public string PdtToken { get; set; }
        public bool PdtToken_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Paytm.Fields.webhook")]
        public string webhook { get; set; }
        public bool webhook_OverrideForStore { get; set; }

    }
}
