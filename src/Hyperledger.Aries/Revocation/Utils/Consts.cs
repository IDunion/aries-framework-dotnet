using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Utils
{
    public class Consts
    {
        public const string CATEGORY_REV_LIST = "revocation_list";
        public const string CATEGORY_REV_REG_DEF = "revocation_reg_def";
        public const string CATEGORY_REV_REG_DEF_PRIVATE = "revocation_reg_def_private";
        public const string CATEGORY_REV_REG_ISSUER = "revocation_reg_def_issuer";
        public const string STATE_REVOCATION_POSTED = "posted";
        public const string STATE_REVOCATION_PENDING = "pending";
        public const string REV_REG_DEF_STATE_ACTIVE = "active";
    }
}
