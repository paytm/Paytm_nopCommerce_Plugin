using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Paytm
{
    public class PaytmPaymentSettings : ISettings
    {
        public string MerchantId { get; set; }
        public string MerchantKey { get; set; }
        public string Website { get; set; }        
        public string IndustryTypeId { get; set; }
		public string PaymentUrl{ get; set; }  
		public string CallBackUrl{ get; set; }
    }
}
