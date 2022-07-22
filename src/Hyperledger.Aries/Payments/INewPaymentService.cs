using System.Threading.Tasks;
using Hyperledger.Aries.Agents;

namespace Hyperledger.Aries.Payments
{
    /// <summary>
    /// Payment Service Interface
    /// </summary>
    public interface INewPaymentService
    {
        /// <summary>
        /// Gets the default payment address for the given agent context.
        /// </summary>
        /// <param name="agentContext"></param>
        /// <returns></returns>
        Task<PaymentAddressRecord> GetDefaultPaymentAddressAsync(INewAgentContext agentContext);

        /// <summary>
        /// Sets the given address as default payment address.
        /// </summary>
        /// <param name="agentContext"></param>
        /// <param name="addressRecord"></param>
        /// <returns></returns>
        Task SetDefaultPaymentAddressAsync(INewAgentContext agentContext, PaymentAddressRecord addressRecord);

        /// <summary>
        /// Creates a new payment address record.
        /// </summary>
        /// <param name="agentContext"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        Task<PaymentAddressRecord> CreatePaymentAddressAsync(INewAgentContext agentContext, AddressOptions configuration = null);

        /// <summary>
        /// Attaches a payment request to the given agent message.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="agentMessage"></param>
        /// <param name="details">The details of the payment</param>
        /// <param name="payeeAddress">The address this payment will be processed to</param>
        /// <returns>Payment record that can be used to reference this payment</returns>
        Task<PaymentRecord> AttachPaymentRequestAsync(INewAgentContext context, AgentMessage agentMessage, PaymentDetails details, PaymentAddressRecord payeeAddress = null);

        /// <summary>
        /// Attach a payment receipt to the given agent message based on the payment record.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="agentMessage"></param>
        /// <param name="paymentRecord"></param>
        void AttachPaymentReceipt(INewAgentContext context, AgentMessage agentMessage, PaymentRecord paymentRecord);

        /// <summary>
        /// Makes a payment for the given payment record.
        /// </summary>
        /// <param name="agentContext"></param>
        /// <param name="paymentRecord">The payment record</param>
        /// <param name="addressRecord">The address to use to make this payment from. If null,
        /// the default payment address will be used</param>
        /// <returns></returns>
        Task MakePaymentAsync(INewAgentContext agentContext, PaymentRecord paymentRecord, PaymentAddressRecord addressRecord = null);

        /// <summary>
        /// Refresh the payment address sources from the ledger.
        /// </summary>
        /// <param name="agentContext"></param>
        /// <param name="paymentAddress">The address to refresh. If null, the default payment address will be used</param>
        /// <returns></returns>
        Task RefreshBalanceAsync(INewAgentContext agentContext, PaymentAddressRecord paymentAddress = null);

        /// <summary>
        /// Creates a payment info object for the given transaction type. The payment info makes auto discovery of the
        /// fees required by querying the ledger.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="transactionType"></param>
        /// <param name="addressRecord"></param>
        /// <returns></returns>
        Task<TransactionCost> GetTransactionCostAsync(INewAgentContext context, string transactionType, PaymentAddressRecord addressRecord = null);

        /// <summary>
        /// Gets the fees associated with a given transaction type
        /// </summary>
        /// <param name="agentContext"></param>
        /// <param name="transactionType"></param>
        /// <returns></returns>
        Task<ulong> GetTransactionFeeAsync(INewAgentContext agentContext, string transactionType);

        /// <summary>
        /// Verifies the payment record on the ledger by comparing the receipts and amounts
        /// </summary>
        /// <param name="context"></param>
        /// <param name="paymentRecord">The payment record to verify</param>
        /// <returns></returns>
        Task<bool> VerifyPaymentAsync(INewAgentContext context, PaymentRecord paymentRecord);
    }
}
