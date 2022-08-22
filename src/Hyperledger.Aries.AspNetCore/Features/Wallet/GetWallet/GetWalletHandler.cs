namespace Hyperledger.Aries.AspNetCore.Features.Wallets
{
  using Hyperledger.Aries.Configuration;
  using Hyperledger.Aries.Storage;
  using Hyperledger.Aries.Storage.Models;
  using Hyperledger.Indy.WalletApi;
  using MediatR;
  using Microsoft.Extensions.Options;
  using System.Threading;
  using System.Threading.Tasks;

  public class GetWalletHandler : IRequestHandler<GetWalletRequest, GetWalletResponse>
  {
    private readonly IWalletService WalletService;
    private readonly IProvisioningService ProvisioningService;
    private readonly AgentOptions AgentOptions;

    public GetWalletHandler
    (
      IWalletService aWalletService,
      IProvisioningService aProvisioningService,
      IOptions<AgentOptions> aAgentOptions
    )
    {
      WalletService = aWalletService;
      ProvisioningService = aProvisioningService;
      AgentOptions = aAgentOptions.Value;
    }

    public async Task<GetWalletResponse> Handle
    (
      GetWalletRequest aGetWalletRequest,
      CancellationToken aCancellationToken
    )
    {
      AriesStorage ariesStorage = await WalletService.GetWalletAsync(AgentOptions.WalletConfiguration, AgentOptions.WalletCredentials);
      Wallet wallet = ariesStorage.Wallet;

      ProvisioningRecord provisioningRecord = await ProvisioningService.GetProvisioningAsync(ariesStorage);
      var getWalletResponse = new GetWalletResponse(aGetWalletRequest.CorrelationId, provisioningRecord);

      return getWalletResponse;
    }
  }
}
