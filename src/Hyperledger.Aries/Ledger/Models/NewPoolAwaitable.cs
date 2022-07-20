using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Ledger
{
    /// <summary>
    /// Awaitable pool handle.
    /// </summary>
    public struct NewPoolAwaitable
    {
        private readonly Func<Task<IntPtr>> _initializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="NewPoolAwaitable"/> struct.
        /// </summary>
        /// <param name="initializer">Initializer.</param>
        public NewPoolAwaitable(Func<Task<IntPtr>> initializer)
        {
            _initializer = initializer;
        }

        /// <summary>
        /// Gets the awaiter for this instance.
        /// </summary>
        /// <returns>The awaiter.</returns>
        public TaskAwaiter<IntPtr> GetAwaiter()
        {
            return _initializer().GetAwaiter();
        }

        /// <summary>
        /// Create new <see cref="NewPoolAwaitable"/> instance from existing pool handle
        /// </summary>
        /// <returns>The pool awaitable.</returns>
        /// <param name="poolHandle">Pool handle.</param>
        public static NewPoolAwaitable FromPool(IntPtr poolHandle)
        {
            return new NewPoolAwaitable(() => Task.FromResult(poolHandle));
        }
    }
}
