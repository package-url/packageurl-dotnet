// SPDX-License-Identifier: MIT

using System;

namespace PackageUrl
{
    /// <summary>
    /// Exception class intended to be used for PackageURL parsing exceptions.
    /// </summary>
    public class MalformedPackageUrlException : Exception
    {
        /// <summary>
        /// Constructs a <see cref="MalformedPackageUrlException" /> with the
        /// specified detail message.
        /// </summary>
        //  <param name="message">The message that describes the error</param>
        public MalformedPackageUrlException(string message) : base(message)
        { }
    }
}
