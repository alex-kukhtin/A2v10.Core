// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web
{
    public class WebLocalizer : BaseLocalizer
    {
        private readonly ILocalizerDictiorany _dictionary;

        public WebLocalizer(ILocalizerDictiorany dictiorany, ICurrentUser user)
            : base(user)
        {
            _dictionary = dictiorany;
        }

        protected override IDictionary<String, String> GetLocalizerDictionary(String locale)
        {
            return _dictionary.GetLocalizerDictionary(locale);
        }
    }
}
