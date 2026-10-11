// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Concurrent;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* Two files whose 'storage' name each other used to build each other until the stack ran out. The hop
 * that lands on an address still on its own hop is the cycle, refused with both paths. A pair cannot sit
 * in TestApp - AllElementsMetadata walks every file - so the descent is played on EndpointLoad itself.
 */
public class StorageCycleTests
{
    static EndpointLoad NewLoad() => new(new ConcurrentDictionary<String, EndpointMetadata>(), new ConcurrentDictionary<String, TableMetadata>());

    [Fact]
    public void A_hop_back_onto_a_hopping_address_is_a_cycle()
    {
        var load = NewLoad();

        using (load.Hop(null, "catalog", "a", "storage", "catalog", "b"))
        {
            var ex = Assert.Throws<InvalidOperationException>(() => load.Hop(null, "catalog", "b", "storage", "catalog", "a"));
            Assert.Contains("/catalog/b: 'storage' points at /catalog/a", ex.Message);
        }
    }

    [Fact]
    public void Hops_one_after_another_are_not_a_cycle()
    {
        var load = NewLoad();

        using (load.Hop(null, "catalog", "customer", "storage", "catalog", "agent")) { }
        using (load.Hop(null, "catalog", "customer", "storage", "catalog", "agent")) { }
        using (load.Hop(null, "catalog", "supplier", "storage", "catalog", "agent")) { }
    }

    // a second address hopping while another one is on its hop - phase 2 of the first one's target reached it
    [Fact]
    public void Hops_nested_onto_an_owner_are_not_a_cycle()
    {
        var load = NewLoad();

        using (load.Hop(null, "catalog", "customer", "storage", "catalog", "agent"))
        using (load.Hop(null, "catalog", "supplier", "storage", "catalog", "agent")) { }
    }
}
