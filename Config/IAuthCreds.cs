using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComdirectTransactionTracker.Config
{
    public interface IAuthCreds
    {
        string ClientId { get; set; }
        string ClientSecret { get; set; }   
    }
}
