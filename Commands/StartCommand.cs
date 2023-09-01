using ComdirectTransactionTracker.Config;
using ComdirectTransactionTracker.Dtos;
using ComdirectTransactionTracker.Services;
using Config.Net;
using Newtonsoft.Json;
using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace ComdirectTransactionTracker.Commands
{
    public class StartCommand : Command<StartCommand.Settings>
    {
        private const string _baseUrl = "https://api.comdirect.de";

        private volatile ValidComdirectToken token;
        private SemaphoreSlim semaphore = new(1);

        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[username]")]
            public string? Username { get; set; }

            [CommandArgument(1, "[password]")]
            public string? Password { get; set; }
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
        {
            if ((settings.Username + settings.Password).Length == 0) 
            {
                Console.WriteLine("Error: both username and password need to be specified as arguments! \a");
                return 1;
            }

            var authCreds = new ConfigurationBuilder<IAuthCreds>()
                .UseJsonFile("creds.json")
                .Build();

            if ((authCreds.ClientId + authCreds.ClientSecret).Length == 0)
            {
                authCreds.ClientId = "n/a";
                authCreds.ClientSecret = "n/a";
                Console.WriteLine("Error, please supply both the client_id & client_secret in 'creds.json' \a");
                return 1;
            }

            var comdirectCredentials = new ComdirectCredentials()
            {
                ClientId = authCreds.ClientId,
                ClientSecret = authCreds.ClientSecret,
                Username = settings.Username,
                Pin = settings.Password,
            };

            var comdirectAuthService = new ComdirectAuthService(comdirectCredentials)
            {
                SessionId = Guid.NewGuid().ToString()
            };

            // Retrieve first valid auth token

            Console.WriteLine("Running initial flow, do not forget to activate the TAN in your photoTAN App");
            token = comdirectAuthService.RunInitialAsync().GetAwaiter().GetResult();
            Console.WriteLine("First token retrieved: " + token.ToString());

            // /banking/v2/accounts/{accountId}/balances

            GetAccountBalance(settings.Username, comdirectAuthService.SessionId);

            return 0;

            System.Timers.Timer pollTimer, renewalTimer;

            renewalTimer = new()
            {
                Interval = TimeSpan.FromSeconds(int.Parse(token.ExpiresIn) - 1).TotalMilliseconds,
                AutoReset = true,
            };
            renewalTimer.Elapsed += delegate
            {
                // We need to lock on the semaphore, because the polling thread needs equal access to the token
                semaphore.Wait();
                token = comdirectAuthService.RunRefreshTokenFlow(token.RefreshToken).GetAwaiter().GetResult();
                semaphore.Release();
            };
            renewalTimer.Start();

            pollTimer = new()
            {
                Interval = TimeSpan.FromDays(1).TotalMilliseconds,
                AutoReset = true,
            };
            pollTimer.Elapsed += delegate
            {
                semaphore.Wait();
                //GetAccountBalance(settings.Username, comdirectAuthService.SessionId);
                semaphore.Release();
            };
            pollTimer.Start();

            Console.WriteLine("Press any key to exit the program...");
            Console.ReadKey();

            return 0;
        }

        public void GetAccountBalance(string accountId, string sessionId)
        {
            string balanceEndpoint = $"{_baseUrl}/banking/v2/accounts/{accountId}/balances";

            using var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders
               .Accept
               .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var httpRequestInfo = new { clientRequestId = new { sessionId = sessionId, requestId = Guid.NewGuid() } };
            var serializedHttpRequestInfo = JsonConvert.SerializeObject(httpRequestInfo);

            httpClient.DefaultRequestHeaders.Add("x-http-request-info", serializedHttpRequestInfo);

            var request = new HttpRequestMessage(HttpMethod.Get, balanceEndpoint);

            var result = httpClient.Send(request);

            Console.WriteLine(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());

            //var comdirectSessionStatus = JsonConvert.DeserializeObject<List<ComdirectGetSessionStatusResponse>>(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            //return comdirectSessionStatus[0];
        }
    }
}
