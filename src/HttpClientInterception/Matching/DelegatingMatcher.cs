// Copyright (c) Just Eat, 2017. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Net.Http;

namespace JustEat.HttpClientInterception.Matching
{
    /// <summary>
    /// A class representing an implementation of <see cref="RequestMatcher"/> that
    /// delegates to a user-provided delegate. This class cannot be inherited.
    /// </summary>
    internal sealed class DelegatingMatcher : RequestMatcher
    {
        /// <summary>
        /// The user-provided predicate to use to test for a match. This field is read-only.
        /// </summary>
        private readonly Predicate<HttpRequestMessage> _predicate;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatingMatcher"/> class.
        /// </summary>
        /// <param name="predicate">The user-provided delegate to use for matching.</param>
        internal DelegatingMatcher(Predicate<HttpRequestMessage> predicate)
        {
            _predicate = predicate;
        }

        /// <inheritdoc />
        public override bool IsMatch(HttpRequestMessage request) => _predicate(request);
    }
}
