using ComdirectTransactionTracker.Dtos;

namespace ComdirectTransactionTracker.Services
{
    public interface IComdirectAuthService
    {
        Task<ValidComdirectToken> RunInitialAsync();
        Task<ValidComdirectToken> RunRefreshTokenFlow(string refreshToken);
    }
}