using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Features.IssueCredential.Models
{
    public class AriesIssuerCreateCredentialResult
    {
        //
        // Zusammenfassung:
        //     Gets the credential JSON.
        public string CredentialJson { get; }

        //
        // Zusammenfassung:
        //     Gets the revocation identifier.
        //
        // Wert:
        //     The revoc identifier.
        public string RevocId { get; }

        //
        // Zusammenfassung:
        //     Gets the revocation registration delta JSON.
        //    
        public string RevocRegDeltaJson { get; }

        public AriesIssuerCreateCredentialResult(string credentialJson, string revocId, string revocRegDeltaJson)
        {
            CredentialJson = credentialJson;
            RevocId = revocId;
            RevocRegDeltaJson = revocRegDeltaJson;
        }
    }
}
