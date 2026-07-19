namespace SkillFixture.Payments;

public sealed partial class PaymentService
{
    public string ProcessorName => "Fixture Gateway";

    public Task<PaymentReceipt> RefundAsync(string transactionId) =>
        Task.FromResult(new PaymentReceipt(transactionId, 0));

    public Task<PaymentReceipt> RefundAsync(PaymentReceipt receipt) =>
        Task.FromResult(receipt with { Amount = -receipt.Amount });
}
