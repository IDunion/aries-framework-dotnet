using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Revocation.Models
{
    public abstract class Profile
    {
        public string BACKEND_NAME = null;
        public string DEFAULT_NAME = "default";

        private InjectionContext _context;
        private bool _created;
        private string _name;

        public Profile(InjectionContext context = null, string name = null, bool created = false)
        {
            // Initialize a base profile.
            _context = context ?? new InjectionContext();
            _created = created;
            _name = name ?? DEFAULT_NAME;
        }

        public string Backend
        {
            // Accessor for the backend implementation name.
            get { return BACKEND_NAME; }
        }

        public InjectionContext Context
        {
            // Accessor for the injection context.
            get { return _context; }
        }
        public bool Created
        {
            // Accessor for the created flag indicating a new profile.
            get { return _created; }
        }

        public string Name
        {
            // Accessor for the profile name.
            get { return _name; }
        }
        public BaseSettings Settings
        {
            // Accessor for scope-specific settings.
            get { return _context.Settings; }
        }

        public abstract ProfileSession Session(InjectionContext context = null);

        public abstract ProfileSession Transaction(InjectionContext context = null);

       
    }
}
