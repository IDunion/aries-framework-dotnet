using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Hyperledger.Aries.Revocation.Utils
{
    public class BaseError: Exception
    {
    /* Generic exception class which other exceptions should inherit from. */

        public string ErrorCode { get; set; }
            public string[] args { get; set; }

        public BaseError(string message, string errorCode = null) : base(message)
        {
            /* Initialize a BaseError instance. */
            ErrorCode = errorCode ?? null;
        }

        public string Message
        {
            /* Accessor for the error message. */
            get { return (args != null && args.Length > 0) ? args[0].ToString().Trim() : ""; }
        }

        public string RollUp
        {
            /* Accessor for nested error messages rolled into one line. */
            get
            {
                string flatten(Exception exc)
                {
                    return string.Join(".", (
                        Regex.Replace(
                            exc.Data != null && exc.Data.Count > 0 ? exc.Data[0].ToString().Trim() : exc.GetType().Name,
                            @"\n\s*",
                            ". "
                        ).Trim()
                    ).Split('.', 2));
                }

                string line = flatten(this);
                var err = this;
                while (err.InnerException != null)
                {
                    err = (BaseError)err.InnerException;
                    line += $". {flatten(err)}";
                }
                return $"{line.Trim()}.";
            }
        }
    }

    public class ProfileSessionInactiveError : BaseError
    {
        public ProfileSessionInactiveError(string message, string errorCode = null) : base(message, errorCode)
        {
        }
    }

    public class InjectionContextError : BaseError
    {
        public InjectionContextError(string message, string errorCode = null) : base(message, errorCode)
        {
        }
    }
}
