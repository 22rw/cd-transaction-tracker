using ComdirectTransactionTracker.Config;
using ComdirectTransactionTracker.Dtos;
using ComdirectTransactionTracker.Services;
using Config.Net;
using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;

namespace ComdirectTransactionTracker.Commands
{
    public class StartCommand : Command<StartCommand.Settings>
    {
        private volatile ValidComdirectToken token;
        private SemaphoreSlim semaphore = new(1);

        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[username]")]
            public string? Username { get; }

            [CommandArgument(1, "[password]")]
            public string? Password { get; }
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
                // Do something with the api
                semaphore.Release();
            };
            pollTimer.Start();

            Console.WriteLine("Press any key to exit the program...");
            Console.ReadKey();

            return 0;
        }
    }
}
