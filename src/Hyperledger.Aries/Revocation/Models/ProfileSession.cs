using Hyperledger.Aries.Revocation.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Revocation.Models
{
    public abstract class ProfileSession
    {
        // An active connection to the profile management backend.

        private bool _active;
        private bool _awaited;
        private int _entered;
        private InjectionContext _context;
        private Profile _profile;
        private List<Event> _events;

        public ProfileSession(Profile profile, InjectionContext context = null, Dictionary<string, object> settings = null)
        {
            // Initialize a base profile session.
            _active = false;
            _awaited = false;
            _entered = 0;
            _context = (context ?? profile.Context).StartScope("session", settings);
            _profile = profile;
            _events = new List<Event>();
        }

        public bool Active { get { return _active; } }

        public InjectionContext Context { get { return _context; } }

        public BaseSettings Settings { get { return _context.Settings; } }

        public bool IsTransaction { get { return false; } }

        public Profile Profile { get { return _profile; } }

        public abstract Task Teardown(bool commit = false);


   }
}
