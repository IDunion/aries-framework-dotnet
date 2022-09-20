using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Hyperledger.Aries.Ledger.Models
{
    /// <summary>
    /// Awaitable pool handle.
    /// </summary>
    public struct PoolHandleAwaitable
    {
        private readonly Func<Task<IntPtr>> _initializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="PoolHandleAwaitable"/> struct.
        /// </summary>
        /// <param name="initializer">Initializer.</param>
        public PoolHandleAwaitable(Func<Task<IntPtr>> initializer)
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
        /// Create new <see cref="PoolHandleAwaitable"/> instance from existing pool handle
        /// </summary>
        /// <returns>The pool awaitable.</returns>
        /// <param name="poolHandle">Pool handle.</param>
        public static PoolHandleAwaitable FromPool(IntPtr poolHandle)
        {
            return new PoolHandleAwaitable(() => Task.FromResult(poolHandle));
        }
    }
}
