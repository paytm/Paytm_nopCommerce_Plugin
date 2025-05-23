﻿using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.Paytm;

/// <summary>
/// Represents Paytm helper
/// </summary>
public class PaytmHelper
{
    #region Properties

    /// <summary>
    /// Get nopCommerce partner code
    /// </summary>
    public static string NopCommercePartnerCode => "nopCommerce_SP";

    /// <summary>
    /// Get the generic attribute name that is used to store an order total that actually sent to Paytm (used to PDT order total validation)
    /// </summary>
    public static string OrderTotalSentToPaytm => "OrderTotalSentToPaytm";

    #endregion

    #region Methods

    /// <summary>
    /// Gets a payment status
    /// </summary>
    /// <param name="paymentStatus">Paytm payment status</param>
    /// <param name="pendingReason">Paytm pending reason</param>
    /// <returns>Payment status</returns>
    public static PaymentStatus GetPaymentStatus(string paymentStatus, string pendingReason)
    {
        var result = PaymentStatus.Pending;

        paymentStatus ??= string.Empty;

        pendingReason ??= string.Empty;

        switch (paymentStatus.ToLowerInvariant())
        {
            case "pending":
                result = (pendingReason.ToLowerInvariant()) switch
                {
                    "authorization" => PaymentStatus.Authorized,
                    _ => PaymentStatus.Pending,
                };
                break;
            case "processed":
            case "completed":
            case "canceled_reversal":
                result = PaymentStatus.Paid;
                break;
            case "denied":
            case "expired":
            case "failed":
            case "voided":
                result = PaymentStatus.Voided;
                break;
            case "refunded":
            case "reversed":
                result = PaymentStatus.Refunded;
                break;
            default:
                break;
        }

        return result;
    }

    #endregion
}