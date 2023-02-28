using Flurl;
using Hyperledger.Aries.Extensions;
using Hyperledger.Aries.Features.OpenId4VCI.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Hyperledger.Aries.Features.OpenId4VCI
{
    public interface IOpenId4VCIService
    {
        public Task ProcessCredentialOffer(string offer);
    }
}
