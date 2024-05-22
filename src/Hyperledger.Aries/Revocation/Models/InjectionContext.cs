using anoncreds_rs_dotnet;
using Hyperledger.Aries.Revocation.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public class InjectionContext : BaseInjector<InjectionContext>
    {
        public const string ROOT_SCOPE = "application";

        private Injector _injector;
        private string _scopeName;
        private List<Scope> _scopes;

        public InjectionContext(Settings settings = null, bool enforceTyping = true)
        {
            _injector = new Injector(settings, enforceTyping);
            _scopeName = ROOT_SCOPE;
            _scopes = new List<Scope>();
        }

        public Injector Injector
        {
            get { return _injector; }
            set { _injector = value; }
        }

        public string ScopeName
        {
            get { return _scopeName; }
            set { _scopeName = value; }
        }

        public Settings Settings
        {
            get { return _injector.Settings; }
        }

        public InjectionContext StartScope(string scope_name, Dictionary<string, object> settings = null)
        {
            if (string.IsNullOrEmpty(scope_name))
            {
                throw new InjectionContextError("Scope name must be non-empty");
            }
            if (_scopeName == scope_name)
            {
                throw new InjectionContextError("Cannot re-enter scope: " + scope_name);
            }
            foreach (Scope scope in _scopes)
            {
                if (scope.Name == scope_name)
                {
                    throw new InjectionContextError("Cannot re-enter scope: " + scope_name);
                }
            }
            InjectionContext result = (InjectionContext)Copy();
            _scopes.Add(new Scope(scope_name, _injector));
            _scopeName = scope_name;
            if (settings != null)
            {
                result.UpdateSettings(settings);
            }
            return result;
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            // Update the scope with additional settings
            if (settings != null)
            {
                this.Injector.Settings.Update(settings);
            }
        }
        public override InjectionContext Inject(InjectionContext baseCls, IDictionary<string, object> settings = null)
        {
            throw new NotImplementedException();
        }

        public override InjectionContext InjectOr(InjectionContext baseCls, IDictionary<string, object> settings = null)
        {
            throw new NotImplementedException();
        }

        public override BaseInjector<InjectionContext> Copy()
        {
            // Produce a copy of the injector instance.
            InjectionContext result = (InjectionContext)this.MemberwiseClone();
            result._injector = _injector;
            result._scopes = this._scopes;
            return result;
        }
    }

    public class Scope
    {
        public string Name { get; set; }
        public Injector Injector { get; set; }

        public Scope(string name, Injector injector)
        {
            Name = name;
            Injector = injector;
        }
    }
}
