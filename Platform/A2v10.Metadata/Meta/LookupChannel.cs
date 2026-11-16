// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Metadata;

/* How what was typed into a channel meets the column. Contains - a substring, a list of
 * candidates to pick from (a selector showing the channel). Equal - the whole value, one record or
 * none (a text box and an exact search), and so only on a unique column.
 */
public enum LookupMatch
{
    Contains,
    Equal
}

// one entry of the target's 'lookup', resolved against its shape (TableMetadata.SetDefaults)
public sealed record LookupChannel(TableColumn Column, LookupMatch Match);
