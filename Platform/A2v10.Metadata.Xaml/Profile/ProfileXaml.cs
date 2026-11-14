// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    // the profile's slots, each by the card's own editor: a reference picks from its catalog as on any card
    internal IReadOnlyList<UIElementBase> SlotEditors() =>
        [.. SqlBuilderProfile.Slots(Table).Select(c => Editor(c, Card, Endpoint.Declaration.Inherits))];
}
