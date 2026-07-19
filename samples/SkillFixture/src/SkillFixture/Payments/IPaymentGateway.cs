namespace SkillFixture.Payments;

public interface IPaymentGateway
{
    Task<string> ChargeAsync(decimal amount, CancellationToken cancellationToken);
}
