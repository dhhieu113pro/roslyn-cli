using SkillFixture.Domain;

namespace SkillFixture.Payments;

public sealed partial class PaymentService(IPaymentGateway gateway)
{
    public event Action<PaymentReceipt>? PaymentCompleted;

    public async Task<PaymentReceipt> ProcessPaymentAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var transactionId = await gateway.ChargeAsync(order.Total, cancellationToken);
        var receipt = new PaymentReceipt(transactionId, order.Total);
        PaymentCompleted?.Invoke(receipt);
        return receipt;
    }
}
