﻿namespace Hyperledger.Aries.Utils
{
    /// <summary>
    /// A collection of tag constants used within the sdk
    /// as tags on wallet records
    /// </summary>
    public static class TagConstants
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public const string Nonce = "nonce";

        public const string Did = "did";

        public const string Alias = "alias";

        public const string ConnectionKey = "connectionKey";

        public const string IssuerDid = "issuerDid";

        public const string InvitationKey = "InvitationKey";

        public const string AutoAcceptConnection = "autoAcceptConnection";

        public const string Role = "role";

        public const string Issuer = "issuer";

        public const string Holder = "holder";

        public const string Requestor = "requestor";
        
        public const string LastThreadId = "threadId";

        public const string ParentThreadId = "parentThreadId";

        public const string UsePublicDid = "usePublicDid";

        public const string RevRegDefJson = "revocationRegistryDefinitionJson";

        public const string RevRegDefPrivateJson = "revocationRegistryDefinitionPrivateJson";

        public const string RevRegJson = "revocationRegistryJson";

        public const string RevRegDeltaJson = "revocationRegistryDeltaJson";

        public const string CredDefJson = "credentialDefinitionJson";

        public const string CredDefPrivateJson = "credentialDefinitionPrivateJson";

        public const string KeyCorrectnesProofJson = "keyCorrectnesProofJson";

        public const string CredJson = "credentialJson";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    }
}
