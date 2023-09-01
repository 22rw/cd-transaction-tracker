using ComdirectTransactionTracker.Commands;
using ComdirectTransactionTracker.Config;
using Config.Net;
using Spectre.Console.Cli;
using System.Timers;

namespace ComdirectTransactionTracker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandApp<StartCommand>();   
            app.Run(args);
        }
    }
}