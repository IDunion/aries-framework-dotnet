// Technology stack: C#
// Framework: .NET

using anoncreds_rs_dotnet.Models;
using Hyperledger.Aries.Ledger.Models;
using Hyperledger.Aries.Revocation.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public abstract class BaseAnonCredsRegistrar
{
    // Base Anon Creds Handler.

    public abstract Regex SupportedIdentifiersRegex { get; }

    public async Task<bool> SupportsAsync(string identifier)
    {
        // Determine whether this registry supports the given identifier.
        return SupportedIdentifiersRegex.IsMatch(identifier);
    }

    /// <summary>
    /// Register a revocation registry definition on the registry.
    /// </summary>
    public abstract Task<RevRegDefResult> RegisterRevocationRegistryDefinition(RevocationRegistryDefinition revocationRegistryDefinition);

  
}
