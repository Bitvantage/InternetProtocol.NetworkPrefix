/*
   Bitvantage.InternetProtocol.Network
   Copyright (C) 2024 Michael Crino
   
   This program is free software: you can redistribute it and/or modify
   it under the terms of the GNU Affero General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.
   
   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU Affero General Public License for more details.
   
   You should have received a copy of the GNU Affero General Public License
   along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;
using System.Xml.Serialization;
using Bitvantage.InternetProtocol;

namespace Test;

public class NetworkPrefixTests
{
    public static ReadOnlyCollection<NetworkPrefixTestRecord> TestAddresses = new List<NetworkPrefixTestRecord>
    {
        new("10.0.0.0", 16, "10.0.0.0", "255.255.0.0", "10.0.255.255", "0.0.255.255", "10.0.0.1", "10.0.255.254"),
        new("10.0.2.3", 16, "10.0.0.0", "255.255.0.0", "10.0.255.255", "0.0.255.255", "10.0.0.1", "10.0.255.254"),
        new("10.0.2.0", 30, "10.0.2.0", "255.255.255.252", "10.0.2.3", "0.0.0.3", "10.0.2.1", "10.0.2.2"),
        new("10.0.2.3", 30, "10.0.2.0", "255.255.255.252", "10.0.2.3", "0.0.0.3", "10.0.2.1", "10.0.2.2"),
        new("10.0.2.3", 31, "10.0.2.2", "255.255.255.254", "10.0.2.3", "0.0.0.1", "10.0.2.2", "10.0.2.3"),
        new("10.0.2.3", 32, "10.0.2.3", "255.255.255.255", "10.0.2.3", "0.0.0.0", "10.0.2.3", "10.0.2.3")
    }.AsReadOnly();


    [Test]
    public void Addition_IPv4_01()
    {
        Assert.That(NetworkPrefix.Parse("10.20.30.0/24") + 5, Is.EqualTo(NetworkPrefix.Parse("10.20.35.0/24")));
    }

    [Test]
    public void Addition_IPv4_02()
    {
        Assert.That(NetworkPrefix.Parse("255.255.255.0/28") + 12, Is.EqualTo(NetworkPrefix.Parse("255.255.255.192/28")));
    }

    [Test]
    public void Addition_IPv4_03()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (NetworkPrefix.Parse("255.255.255.0/28") + 16).ToString());
    }

    [Test]
    public void Addition_IPv4_04()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (NetworkPrefix.Parse("255.255.255.0/28") + UInt128.MaxValue).ToString());
    }

    [Test]
    public void Addresses_IPv4_01()
    {
        var networks =
            NetworkPrefix
                .Parse("10.0.0.0/31")
                .Addresses()
                .ToList();

        var expectedResult = new List<IPAddress>
        {
            IPAddress.Parse("10.0.0.0"),
            IPAddress.Parse("10.0.0.1")
        };

        Assert.That(networks, Is.EqualTo(expectedResult));
    }

    [Test]
    public void Addresses_IPv4_02()
    {
        var networks =
            NetworkPrefix
                .Parse("10.0.0.0/32")
                .Addresses()
                .ToList();

        var expectedResult = new List<IPAddress>
        {
            IPAddress.Parse("10.0.0.0")
        };

        Assert.That(networks, Is.EqualTo(expectedResult));
    }

    [Test]
    public void Addresses_IPv4_03()
    {
        var networks =
            NetworkPrefix
                .Parse("10.0.0.0/30")
                .Addresses()
                .ToList();

        var expectedResult = new List<IPAddress>
        {
            IPAddress.Parse("10.0.0.0"),
            IPAddress.Parse("10.0.0.1"),
            IPAddress.Parse("10.0.0.2"),
            IPAddress.Parse("10.0.0.3")
        };

        Assert.That(networks, Is.EqualTo(expectedResult));
    }

    [Test]
    public void Addresses_IPv4_04()
    {
        var networks =
            NetworkPrefix
                .Parse("10.0.0.0/28")
                .Addresses()
                .ToList();

        var expectedResult = new List<IPAddress>
        {
            IPAddress.Parse("10.0.0.0"),
            IPAddress.Parse("10.0.0.1"),
            IPAddress.Parse("10.0.0.2"),
            IPAddress.Parse("10.0.0.3"),
            IPAddress.Parse("10.0.0.4"),
            IPAddress.Parse("10.0.0.5"),
            IPAddress.Parse("10.0.0.6"),
            IPAddress.Parse("10.0.0.7"),
            IPAddress.Parse("10.0.0.8"),
            IPAddress.Parse("10.0.0.9"),
            IPAddress.Parse("10.0.0.10"),
            IPAddress.Parse("10.0.0.11"),
            IPAddress.Parse("10.0.0.12"),
            IPAddress.Parse("10.0.0.13"),
            IPAddress.Parse("10.0.0.14"),
            IPAddress.Parse("10.0.0.15")
        };

        Assert.That(networks, Is.EqualTo(expectedResult));
    }


    [Test]
    public void Allocation_IPv4_01()
    {
        Assert.That(NetworkPrefix.Parse("10.20.30.0/24").Allocation, Is.EqualTo(AddressAllocation.Private));
    }

    [Test]
    public void Allocation_IPv4_02()
    {
        Assert.That(NetworkPrefix.Parse("14.20.30.0/24").Allocation, Is.EqualTo(AddressAllocation.Global));
    }

    [Test]
    public void Class_IPv4_01()
    {
        Assert.That(NetworkPrefix.Parse("10.20.30.0/24").Class, Is.EqualTo(NetworkClass.A));
    }

    [Test]
    public void Class_IPv4_02()
    {
        Assert.That(NetworkPrefix.Parse("192.168.100.0/24").Class, Is.EqualTo(NetworkClass.C));
    }

    [Test]
    public void Class_IPv4_03()
    {
        Assert.That(NetworkPrefix.Parse("255.255.255.255/32").Class, Is.EqualTo(NetworkClass.E));
    }

    [Test]
    public void CompareTo_IPv4_01()
    {
        var network1 = NetworkPrefix.Parse("10.0.0.0/16");
        var network2 = NetworkPrefix.Parse("10.1.0.0/16");

        Assert.That(network1.CompareTo(network2), Is.EqualTo(-1));
    }

    [Test]
    public void CompareTo_IPv4_02()
    {
        var network1 = NetworkPrefix.Parse("10.0.0.0/16");
        var network2 = NetworkPrefix.Parse("10.1.0.0/16");

        Assert.That(network2.CompareTo(network1), Is.EqualTo(1));
    }

    [Test]
    public void CompareTo_IPv4_03()
    {
        var network1 = NetworkPrefix.Parse("10.1.0.0/16");
        var network2 = NetworkPrefix.Parse("10.1.0.0/16");

        Assert.That(network1.CompareTo(network2), Is.EqualTo(0));
    }

    [Test]
    public void CompareTo_IPv4_04()
    {
        var network1 = NetworkPrefix.Parse("10.1.0.0/16");
        var network2 = NetworkPrefix.Parse("10.1.0.0/17");

        Assert.That(network1.CompareTo(network2), Is.EqualTo(-1));
    }

    [Test]
    public void CompareTo_IPv4_05()
    {
        var networks = new List<NetworkPrefix?>
            {
                null,
                NetworkPrefix.Parse("10.0.128.0/17"),
                NetworkPrefix.Parse("10.0.64.0/18"),
                NetworkPrefix.Parse("10.0.32.0/19"),
                NetworkPrefix.Parse("10.0.16.0/20"),
                NetworkPrefix.Parse("10.0.0.0/21"),
                NetworkPrefix.Parse("10.0.12.0/22"),
                NetworkPrefix.Parse("10.0.8.0/23"),
                NetworkPrefix.Parse("10.0.11.0/24")
            }.OrderBy(item => item)
            .ToArray();

        var expectedResult = new List<NetworkPrefix?>
        {
            null,
            NetworkPrefix.Parse("10.0.0.0/21"),
            NetworkPrefix.Parse("10.0.8.0/23"),
            NetworkPrefix.Parse("10.0.11.0/24"),
            NetworkPrefix.Parse("10.0.12.0/22"),
            NetworkPrefix.Parse("10.0.16.0/20"),
            NetworkPrefix.Parse("10.0.32.0/19"),
            NetworkPrefix.Parse("10.0.64.0/18"),
            NetworkPrefix.Parse("10.0.128.0/17")
        };

        Assert.That(networks, Is.EqualTo(expectedResult));
    }

    [Test]
    public void ComplementaryNetwork_IPv4_01()
    {
        var network1 = NetworkPrefix.Parse("10.0.10.0/24");

        Assert.That(network1.ComplementaryPrefix, Is.EqualTo(NetworkPrefix.Parse("10.0.11.0/24")));
    }

    [Test]
    public void ComplementaryNetwork_IPv4_02()
    {
        var network1 = NetworkPrefix.Parse("10.0.11.0/24");

        Assert.That(network1.ComplementaryPrefix, Is.EqualTo(NetworkPrefix.Parse("10.0.10.0/24")));
    }

    [Test]
    public void ComplementaryNetwork_IPv4_03()
    {
        Assert.Throws<ArgumentException>(() => _ = NetworkPrefix.Parse("0.0.0.0/0").ComplementaryPrefix);
    }

    [Test]
    public void ComplementaryNetwork_IPv4_04()
    {
        var network = NetworkPrefix.Parse("10.0.0.0/16");

        Assert.That(network.ComplementaryPrefix, Is.EqualTo(NetworkPrefix.Parse("10.1.0.0/16")));
    }

    [Test]
    public void ComplementaryNetwork_IPv4_05()
    {
        var network = NetworkPrefix.Parse("10.1.0.0/16");

        Assert.That(network.ComplementaryPrefix, Is.EqualTo(NetworkPrefix.Parse("10.0.0.0/16")));
    }

    [Test]
    public void Constructor_01()
    {
        var network = NetworkPrefix.Parse("10.1.2.3/8");

        Assert.That(network.Address, Is.EqualTo(IPAddress.Parse("10.0.0.0")));
    }

    [Test]
    public void Constructor_02()
    {
        foreach (var testAddress in TestAddresses)
        {
            var ipNetwork = new NetworkPrefix(testAddress.Address, testAddress.Prefix);

            Assert.That(ipNetwork.Address, Is.EqualTo(testAddress.Network));
            Assert.That(ipNetwork.Length, Is.EqualTo(testAddress.Prefix));
            Assert.That(ipNetwork.Mask, Is.EqualTo(testAddress.Mask));
            Assert.That(ipNetwork.Broadcast, Is.EqualTo(testAddress.Broadcast));
            Assert.That(ipNetwork.Wildcard, Is.EqualTo(testAddress.Wildcard));
            Assert.That(ipNetwork.FirstHost, Is.EqualTo(testAddress.FirstHost));
            Assert.That(ipNetwork.LastHost, Is.EqualTo(testAddress.LastHost));
        }
    }

    [Test]
    public void ContainedByOrEqual_IPv4_01()
    {
        var largerNetwork = NetworkPrefix.Parse("131.192.0.0/10");

        Assert.That(largerNetwork.ContainsOrEqual(NetworkPrefix.Parse("192.0.0.0/2")), Is.False);
    }

    [Test]
    public void Contains_IPv4_01()
    {
        var largerNetwork = NetworkPrefix.Parse("0.0.0.0/1");

        Assert.That(largerNetwork.Contains(NetworkPrefix.Parse("116.11.0.0/16")), Is.True);
    }

    [Test]
    public void Contains_IPv4_02()
    {
        var largerNetwork = NetworkPrefix.Parse("10.20.0.0/16");

        Assert.That(largerNetwork.Contains(NetworkPrefix.Parse("10.20.0.0/16")), Is.False);
        Assert.That(largerNetwork.Contains(NetworkPrefix.Parse("10.20.0.0/24")), Is.True);
        Assert.That(largerNetwork.Contains(NetworkPrefix.Parse("10.20.255.0/24")), Is.True);
        Assert.That(largerNetwork.Contains(NetworkPrefix.Parse("10.20.30.40/32")), Is.True);
        Assert.That(largerNetwork.Contains(NetworkPrefix.Parse("10.21.0.0/24")), Is.False);
        Assert.That(largerNetwork.Contains(NetworkPrefix.Parse("10.20.0.0/15")), Is.False);
    }

    [Test]
    public void ContainsOrEqual_IPv4_03()
    {
        var largerNetwork = NetworkPrefix.Parse("10.20.0.0/16");

        Assert.That(largerNetwork.ContainsOrEqual(NetworkPrefix.Parse("10.20.0.0/16")), Is.True);
        Assert.That(largerNetwork.ContainsOrEqual(NetworkPrefix.Parse("10.20.0.0/24")), Is.True);
        Assert.That(largerNetwork.ContainsOrEqual(NetworkPrefix.Parse("10.20.255.0/24")), Is.True);
        Assert.That(largerNetwork.ContainsOrEqual(NetworkPrefix.Parse("10.20.30.40/32")), Is.True);
        Assert.That(largerNetwork.ContainsOrEqual(NetworkPrefix.Parse("10.21.0.0/24")), Is.False);
        Assert.That(largerNetwork.ContainsOrEqual(NetworkPrefix.Parse("10.20.0.0/15")), Is.False);
    }


    [Test]
    public void Equals_IPv4_01()
    {
        Assert.That(NetworkPrefix.Parse("10.20.30.0/24"), Is.EqualTo(NetworkPrefix.Parse("10.20.30.0/24")));
    }

    [Test]
    public void Equals_IPv4_02()
    {
        var network = NetworkPrefix.Parse("10.20.30.0/24");
        Assert.That(network, Is.EqualTo(network));
    }

    [Test]
    public void Equals_IPv4_03()
    {
        Assert.That(NetworkPrefix.Parse("10.20.30.0/24"), Is.Not.EqualTo(NetworkPrefix.Parse("10.20.36.0/24")));
    }


    [Test]
    public void Equals_IPv4_04()
    {
        var network1 = NetworkPrefix.Parse("10.0.0.0/16");
        var network2 = NetworkPrefix.Parse("10.0.0.0/16");

        Assert.That(network1, Is.EqualTo(network2));
    }

    [Test]
    public void GetContainingNetwork_IPv4_01()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork("10.0.0.0/24", "10.0.1.0/24"), Is.EqualTo((NetworkPrefix)"10.0.0/23"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_02()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork("200.0.0.0/24", "10.0.1.0/24"), Is.EqualTo((NetworkPrefix)"0.0.0.0/0"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_03()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork("10.0.1.0/24", "10.0.1.0/24"), Is.EqualTo((NetworkPrefix)"10.0.1.0/24"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_04()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork("10.0.1.0/24", "10.0.1.0/23"), Is.EqualTo((NetworkPrefix)"10.0.1.0/23"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_05()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork("10.0.1.0/32", "0.0.0.0/0"), Is.EqualTo((NetworkPrefix)"0.0.0.0/0"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_06()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork("0.0.0.0/1", "0.0.0.0/2"), Is.EqualTo((NetworkPrefix)"0.0.0.0/1"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_07()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork("0.0.0.0/2", "255.255.255.0/24"), Is.EqualTo((NetworkPrefix)"0.0.0.0/0"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_08()
    {
        Assert.That(NetworkPrefix.GetContainingNetwork(new NetworkPrefix[] { "1.0.0.0/8", "2.0.0.0/8", "3.0.0.0/8" }), Is.EqualTo((NetworkPrefix)"0.0.0.0/6"));
    }

    [Test]
    public void GetContainingNetwork_IPv4_09()
    {
        var networks = new NetworkPrefix[]
        {
            "10.0.0.0/24",
            "10.0.1.0/24",
            "10.0.2.0/24",
            "10.0.3.0/24",
            "254.0.0.0/24"
        };

        var containingNetwork = NetworkPrefix.GetContainingNetwork(networks);

        Assert.That(containingNetwork, Is.EqualTo(NetworkPrefix.Parse("0.0.0.0/0")));
    }

    [Test]
    public void GetContainingNetwork_IPv4_10()
    {
        var networks = new NetworkPrefix[]
        {
            "10.0.0.0/24",
            "10.0.1.0/24",
            "10.0.2.0/24",
            "10.0.3.0/24"
        };

        var containingNetwork = NetworkPrefix.GetContainingNetwork(networks);

        Assert.That(containingNetwork, Is.EqualTo(NetworkPrefix.Parse("10.0.0.0/22")));
    }

    [Test]
    public void GetContainingNetwork_IPv4_11()
    {
        var networks = new NetworkPrefix[]
        {
            "10.0.0.0/24",
            "10.0.1.0/24",
            "10.0.2.0/24",
            "10.0.3.0/24",
            "127.255.255.0/24"
        };

        var containingNetwork = NetworkPrefix.GetContainingNetwork(networks);

        Assert.That(containingNetwork, Is.EqualTo(NetworkPrefix.Parse("0.0.0.0/1")));
    }

    [Test]
    public void GreaterThan_IPv4_01()
    {
        var network1 = NetworkPrefix.Parse("10.0.0.0/16");
        var network2 = NetworkPrefix.Parse("10.1.0.0/16");

        Assert.That(network2, Is.GreaterThan(network1));
    }

    [Test]
    public void ImplicitConversion_01()
    {
        NetworkPrefix actual = "192.168.13.0/24";
        var expected = new NetworkPrefix(new IPAddress(new byte[] { 192, 168, 13, 0 }), 24);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ImplicitConversion_02()
    {
        string actual = new NetworkPrefix(new IPAddress(new byte[] { 192, 168, 13, 0 }), 24);
        var expected = "192.168.13.0/24";

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void JsonConverter_01()
    {
        var network = NetworkPrefix.Parse("10.20.30.40");

        var actual = JsonSerializer.Serialize(network);

        var expected =
            """
            "10.20.30.40/32"
            """;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void JsonConverter_02()
    {
        var networks = new List<NetworkPrefix> { "10.20.30.40", "5.10.15.20" };

        var actual = JsonSerializer.Serialize(networks);

        var expected =
            """
            ["10.20.30.40/32","5.10.15.20/32"]
            """;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void JsonConverter_03()
    {
        var json =
            """
            "10.20.30.40/32"
            """;

        var actual = JsonSerializer.Deserialize<NetworkPrefix>(json);
        var expected = NetworkPrefix.Parse("10.20.30.40");

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void JsonConverter_04()
    {
        var json =
            """
            ["10.20.30.40/32","5.10.15.20/32"]
            """;

        var actual = JsonSerializer.Deserialize<List<NetworkPrefix>>(json);
        var expected = new List<NetworkPrefix> { "10.20.30.40", "5.10.15.20" };

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void LessThan_IPv4_01()
    {
        var network1 = NetworkPrefix.Parse("10.0.0.0/16");
        var network2 = NetworkPrefix.Parse("10.1.0.0/16");

        Assert.That(network1, Is.LessThan(network2));
    }

    [Test]
    public void Parse_IPv4_01()
    {
        foreach (var testAddress in TestAddresses)
        {
            var ipNetwork = NetworkPrefix.Parse($"{testAddress.Address}/{testAddress.Prefix}");

            Assert.That(ipNetwork.Address, Is.EqualTo(testAddress.Network));
            Assert.That(ipNetwork.Length, Is.EqualTo(testAddress.Prefix));
            Assert.That(ipNetwork.Mask, Is.EqualTo(testAddress.Mask));
            Assert.That(ipNetwork.Broadcast, Is.EqualTo(testAddress.Broadcast));
            Assert.That(ipNetwork.Wildcard, Is.EqualTo(testAddress.Wildcard));
            Assert.That(ipNetwork.FirstHost, Is.EqualTo(testAddress.FirstHost));
            Assert.That(ipNetwork.LastHost, Is.EqualTo(testAddress.LastHost));
        }
    }

    [Test]
    public void Parse_IPv4_02()
    {
        var ipNetwork = NetworkPrefix.Parse("one.one.one.one/32");
        Assert.That(ipNetwork, Is.AnyOf(NetworkPrefix.Parse("1.1.1.1/32"), NetworkPrefix.Parse("1.0.0.1/32")));
    }

    [Test]
    public void Parse_IPv4_03()
    {
        var ipNetwork = NetworkPrefix.Parse("one.one.one.one 255.255.255.255");
        Assert.That(ipNetwork, Is.AnyOf(NetworkPrefix.Parse("1.1.1.1/32"), NetworkPrefix.Parse("1.0.0.1/32")));
    }

    [Test]
    public void Parse_IPv4_04()
    {
        foreach (var testAddress in TestAddresses)
        {
            var ipNetwork = NetworkPrefix.Parse($"{testAddress.Address} {testAddress.Mask}");

            Assert.That(ipNetwork.Address, Is.EqualTo(testAddress.Network));
            Assert.That(ipNetwork.Length, Is.EqualTo(testAddress.Prefix));
            Assert.That(ipNetwork.Mask, Is.EqualTo(testAddress.Mask));
            Assert.That(ipNetwork.Broadcast, Is.EqualTo(testAddress.Broadcast));
            Assert.That(ipNetwork.Wildcard, Is.EqualTo(testAddress.Wildcard));
            Assert.That(ipNetwork.FirstHost, Is.EqualTo(testAddress.FirstHost));
            Assert.That(ipNetwork.LastHost, Is.EqualTo(testAddress.LastHost));
        }
    }

    [Test]
    public void RemoveNetwork_IPv4_01()
    {
        var network = NetworkPrefix.Parse("10.0.0.0/16");

        var expectedResult = new List<NetworkPrefix>
        {
            NetworkPrefix.Parse("10.0.128.0/17"),
            NetworkPrefix.Parse("10.0.64.0/18"),
            NetworkPrefix.Parse("10.0.32.0/19"),
            NetworkPrefix.Parse("10.0.16.0/20"),
            NetworkPrefix.Parse("10.0.0.0/21"),
            NetworkPrefix.Parse("10.0.12.0/22"),
            NetworkPrefix.Parse("10.0.8.0/23"),
            NetworkPrefix.Parse("10.0.11.0/24")
        };

        Assert.That(network.RemoveNetwork(NetworkPrefix.Parse("10.0.10.0/24")), Is.EqualTo(expectedResult));
    }


    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Split_IPv4_01()
    {
        var ipNetwork = NetworkPrefix.Parse("10.20.30.0/24");
        var split = ipNetwork.Split();

        var result = new[]
        {
            NetworkPrefix.Parse("10.20.30.64/25"),
            NetworkPrefix.Parse("10.20.30.128/25")
        };

        Assert.That(split, Is.EqualTo(result));
    }

    [Test]
    public void Split_IPv4_02()
    {
        var ipNetwork = NetworkPrefix.Parse("10.20.30.0/24");
        var split = ipNetwork.Split(26);

        var result = new[]
        {
            NetworkPrefix.Parse("10.20.30.0/26"),
            NetworkPrefix.Parse("10.20.30.64/26"),
            NetworkPrefix.Parse("10.20.30.128/26"),
            NetworkPrefix.Parse("10.20.30.192/26")
        };

        Assert.That(split, Is.EqualTo(result));
    }

    [Test]
    public void Split_IPv4_03()
    {
        var ipNetwork = NetworkPrefix.Parse("10.20.30.40/32");

        Assert.Throws<InvalidOperationException>(() => ipNetwork.Split().ToArray());
    }

    [Test]
    public void Subtraction_IPv4_01()
    {
        Assert.That(NetworkPrefix.Parse("10.20.30.0/24") - 15, Is.EqualTo(NetworkPrefix.Parse("10.20.15.0/24")));
    }

    [Test]
    public void Subtraction_IPv4_02()
    {
        Assert.That(NetworkPrefix.Parse("128.0.0.0/1") - 1, Is.EqualTo(NetworkPrefix.Parse("0.0.0.0/1")));
    }

    [Test]
    public void Subtraction_IPv4_03()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (NetworkPrefix.Parse("0.0.0.0/24") - 1).ToString());
    }

    [Test]
    public void Subtraction_IPv4_04()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (NetworkPrefix.Parse("0.0.0.0/24") - UInt128.MaxValue).ToString());
    }

    [Test]
    public void Summarize_IPv4()
    {
        var networks = new List<NetworkPrefix>
        {
            NetworkPrefix.Parse("0.0.0.0/0"),

            NetworkPrefix.Parse("10.0.8.0/23"),
            NetworkPrefix.Parse("10.0.10.0/24"),
            NetworkPrefix.Parse("10.0.11.0/24"),
            NetworkPrefix.Parse("10.0.12.0/22"),

            NetworkPrefix.Parse("10.0.128.0/18"),
            NetworkPrefix.Parse("10.0.192.0/18"),

            NetworkPrefix.Parse("100.0.0.100/32"),
            NetworkPrefix.Parse("100.0.0.101/32"),

            NetworkPrefix.Parse("100.0.0.102/32")
        };

        var expectedResult = new List<NetworkPrefix>
        {
            NetworkPrefix.Parse("0.0.0.0/0"),
            NetworkPrefix.Parse("10.0.128.0/17"),
            NetworkPrefix.Parse("10.0.8.0/21"),
            NetworkPrefix.Parse("100.0.0.100/31"),
            NetworkPrefix.Parse("100.0.0.102/32")
        };

        Assert.That(NetworkPrefix.Summarize(networks).ToList(), Is.EqualTo(expectedResult));
    }

    [Test]
    public void ToString_IPv4_01()
    {
        var ipNetwork = NetworkPrefix.Parse("10.20.30.0/24");
        var value = ipNetwork.ToString(NetworkFormat.Detail);
    }

    [Test]
    public void ToString_IPv4_02()
    {
        var network = NetworkPrefix.Parse("10.20.30.0/24");

        var actual = network.ToString(NetworkFormat.Detail);
        var expected =
            """
            Address:    10.20.30.0     00001010.00010100.00011110.00000000
            Mask:       255.255.255.0  11111111.11111111.11111111.00000000
            Wildcard:   0.0.0.255      00000000.00000000.00000000.11111111
            Broadcast:  10.20.30.255   00001010.00010100.00011110.11111111
            First Host: 10.20.30.1     00001010.00010100.00011110.00000001
            Last Host:  10.20.30.254   00001010.00010100.00011110.11111110
            Hosts/Net:  254

            """;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ToString_IPv6_01()
    {
        var network = NetworkPrefix.Parse("2001:0db8:0001:0000:0000:0ab9:C0A8:0102/45");

        var actual = network.ToString(NetworkFormat.Detail);
        var expected =
            """
            Address:    2001:db8::                           0010000000000001:0000110110111000:0000000000000000:0000000000000000:0000000000000000:0000000000000000:0000000000000000:0000000000000000
            Mask:       ffff:ffff:fff8::                     1111111111111111:1111111111111111:1111111111111000:0000000000000000:0000000000000000:0000000000000000:0000000000000000:0000000000000000
            Wildcard:   ::7:ffff:ffff:ffff:ffff:ffff         0000000000000000:0000000000000000:0000000000000111:1111111111111111:1111111111111111:1111111111111111:1111111111111111:1111111111111111
            Broadcast:  2001:db8:7:ffff:ffff:ffff:ffff:ffff  0010000000000001:0000110110111000:0000000000000111:1111111111111111:1111111111111111:1111111111111111:1111111111111111:1111111111111111
            First Host: 2001:db8::1                          0010000000000001:0000110110111000:0000000000000000:0000000000000000:0000000000000000:0000000000000000:0000000000000000:0000000000000001
            Last Host:  2001:db8:7:ffff:ffff:ffff:ffff:fffe  0010000000000001:0000110110111000:0000000000000111:1111111111111111:1111111111111111:1111111111111111:1111111111111111:1111111111111110
            Hosts/Net:  9,671,406,556,917,033,397,649,406
            
            """;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void XmlConverter_01()
    {
        var network = NetworkPrefix.Parse("10.20.30.40/15");

        var outputStream = new MemoryStream();
        var xmlSerializer = new XmlSerializer(typeof(NetworkPrefix));
        xmlSerializer.Serialize(outputStream, network);
        outputStream.Seek(0, SeekOrigin.Begin);

        var actual = new StreamReader(outputStream).ReadToEnd();

        var expected =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <NetworkPrefix>10.20.0.0/15</NetworkPrefix>
            """;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void XmlConverter_02()
    {
        var networks = new List<NetworkPrefix> { "10.20.30.40/15", "50.60.70.80/15" };

        var outputStream = new MemoryStream();
        var xmlSerializer = new XmlSerializer(typeof(List<NetworkPrefix>));
        xmlSerializer.Serialize(outputStream, networks);
        outputStream.Seek(0, SeekOrigin.Begin);

        var actual = new StreamReader(outputStream).ReadToEnd();

        var expected =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ArrayOfNetworkPrefix xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <NetworkPrefix>10.20.0.0/15</NetworkPrefix>
              <NetworkPrefix>50.60.0.0/15</NetworkPrefix>
            </ArrayOfNetworkPrefix>
            """;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void XmlConverter_03()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
              <NetworkPrefix>10.20.0.0/15</NetworkPrefix>
            """;

        var inputStream = new StringReader(xml);
        var xmlSerializer = new XmlSerializer(typeof(NetworkPrefix));
        var actual = (NetworkPrefix)xmlSerializer.Deserialize(inputStream);
        var expected = NetworkPrefix.Parse("10.20.30.40/15");

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void XmlConverter_04()
    {
        var xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ArrayOfNetworkPrefix xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <NetworkPrefix>10.20.0.0/15</NetworkPrefix>
              <NetworkPrefix>50.60.0.0/15</NetworkPrefix>
            </ArrayOfNetworkPrefix>
            """;

        var inputStream = new StringReader(xml);
        var xmlSerializer = new XmlSerializer(typeof(List<NetworkPrefix>));
        var actual = (List<NetworkPrefix>)xmlSerializer.Deserialize(inputStream);
        var expected = new List<NetworkPrefix> { "10.20.0.0/15", "50.60.0.0/15" };

        Assert.That(actual, Is.EqualTo(expected));
    }
}