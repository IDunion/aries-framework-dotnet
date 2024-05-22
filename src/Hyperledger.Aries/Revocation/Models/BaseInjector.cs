using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Aries.Revocation.Models
{
    public abstract class BaseInjector<T>
    {
        /// <summary>
        /// Get the provided instance of a given class identifier.
        /// </summary>
        /// <param name="baseCls">The base class to retrieve an instance of</param>
        /// <param name="settings">An optional mapping providing configuration to the provider</param>
        /// <returns>An instance of the base class, or null</returns>
        public abstract T Inject(T baseCls, IDictionary<string, object> settings = null);

        /// <summary>
        /// Get the provided instance of a given class identifier or default if not found.
        /// </summary>
        /// <param name="baseCls">The base class to retrieve an instance of</param>
        /// <param name="settings">An optional mapping providing configuration to the provider</param>
        /// <param name="default">Default return value if no instance is found</param>
        /// <returns>An instance of the base class, or null</returns>
        public abstract T InjectOr(T baseCls, IDictionary<string, object> settings = null);

        /// <summary>
        /// Produce a copy of the injector instance.
        /// </summary>
        /// <returns>A copy of the injector instance</returns>
        public abstract BaseInjector<T> Copy();

       
    }
}
