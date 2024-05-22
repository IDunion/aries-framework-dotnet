using anoncreds_rs_dotnet;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public class Injector : BaseInjector<Injector>
    { 
        private bool enforceTyping;
        private Dictionary<string, object> providers = new Dictionary<string, object>();
        private Settings settings;

        public Injector(Settings settings = null, bool enforceTyping = true)
        {
            this.enforceTyping = enforceTyping;
            this.settings = settings;
        }

        public Settings Settings
        {
            get
            {
                return this.settings;
            }
            set
            {
                this.settings = value;
            }
        }

        public override BaseInjector<Injector> Copy()
        {
            throw new NotImplementedException();
        }

        public override Injector Inject(Injector baseCls, IDictionary<string, object> settings = null)
        {
            throw new NotImplementedException();
        }

        public override Injector InjectOr(Injector baseCls, IDictionary<string, object> settings = null)
        {
            throw new NotImplementedException();
        }
    }
}
