﻿/*
   Bitvantage.InternetProtocol.NetworkPrefix
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

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Bitvantage.InternetProtocol.Converters;

namespace Bitvantage.InternetProtocol;

public enum NetworkFormat
{
    AddressAndPrefix,
    Detail,
    AddressAndMask,
    Address,
    Mask
}

public enum AddressAllocation
{
    Undefined = 0,
    Private = 1,
    Global = 2,
    AutomaticPrivateIPAddressing = 3,
    Loopback = 4,
    CurrentNetwork = 5,
    Multicast = 6,
    Experimental = 7,
    Broadcast = 8
}

public enum NetworkClass
{
    Undefined = 0,
    A = 1,
    B = 2,
    C = 3,
    D = 4,
    E = 5
}

public enum IPVersion
{
    Undefined = 0,
    IPv4 = 1,
    IPv6 = 2
}

[Serializable]
[JsonConverter(typeof(NetworkPrefixJsonConverter))]
public class NetworkPrefix : IComparable<NetworkPrefix>, IXmlSerializable
{
    private static readonly UInt128[] Ipv4HostMaskBits = new UInt128[33];
    private static readonly UInt128[] Ipv6HostMaskBits = new UInt128[129];

    private static readonly UInt128[] Ipv4NetworkMaskBits = new UInt128[33];
    private static readonly UInt128[] Ipv6NetworkMaskBits = new UInt128[129];

    private static readonly IPAddress[] Ipv4HostMaskObjects = new IPAddress[33];
    private static readonly IPAddress[] Ipv6HostMaskObjects = new IPAddress[129];

    private static readonly IPAddress[] Ipv4NetworkMaskObjects = new IPAddress[33];
    private static readonly IPAddress[] Ipv6NetworkMaskObjects = new IPAddress[129];

    private static readonly Dictionary<IPAddress, int> IpHostMaskToPrefix = new();
    private static readonly Dictionary<IPAddress, int> IpNetworkMaskToPrefix = new();

    private static readonly UInt128[] Ipv4HostCount = new UInt128[33];
    private static readonly UInt128[] Ipv6HostCount = new UInt128[129];

    private static readonly BigInteger[] Ipv4AddressCount = new BigInteger[33];
    private static readonly BigInteger[] Ipv6AddressCount = new BigInteger[129];

    private static readonly Dictionary<NetworkPrefix, AddressAllocation> Allocations;
    private static readonly Dictionary<NetworkPrefix, NetworkClass> Classes;


    public ushort AddressLength { get; private set; }
    public UInt128 NetworkBits { get; private set; }

    public AddressAllocation Allocation
    {
        get
        {
            var allocation = Allocations
                .Where(item => item.Key.ContainsOrEqual(this))
                .MaxBy(item => item.Key.Length);
            
            return allocation.Value;
        }
    }

    public IPAddress Broadcast => UInt128ToIpAddress(NetworkBits ^ HostMaskBits);
    public NetworkClass Class
    {
        get
        {
            var @class = Classes
                .Where(item => item.Key.ContainsOrEqual(this))
                .MaxBy(item => item.Key.Length);

            return @class.Value;
        }
    }

    /// <summary>
    ///     Returns the complementary <c>NetworkPrefix</c>.
    ///     A complementary prefix is the prefix that has the same mask and same address, except the least significant bit of
    ///     the address is flipped.
    ///     For example the complementary prefix of 10.1.2.0/25 is 10.1.2.128/25 and vice versa
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the prefix length is 0</exception>
    public NetworkPrefix ComplementaryPrefix
    {
        get
        {
            if (Length == 0)
                throw new ArgumentException($"{this} has no complementary network");

            // XOR the last bit in the network
            var complementaryBits = NetworkBits ^ (UInt128.One << (AddressLength - Length));

            // convert the bits to an IP address
            var complementaryAddress = UInt128ToIpAddress(complementaryBits);

            // construct a new network
            var complementaryNetwork = new NetworkPrefix(complementaryAddress, Length);

            return complementaryNetwork;
        }
    }

    /// <summary>
    ///     Returns the first host address
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Throws when the prefix is zero</exception>
    public IPAddress FirstHost
    {
        get
        {
            // special cases
            // /0's are network addresses, they have no host addresses
            // /31's and /127 have not network or broadcast address and are all host addresses
            // /32's and /128's are host addresses. Host addresses are the first and last address
            if (Length == 0)
                throw new ArgumentOutOfRangeException();

            if (Length >= AddressLength - 1)
                return Address;

            return UInt128ToIpAddress(NetworkBits + 1);
        }
    }

    /// <summary>
    ///     Returns the last host address
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Throws when the prefix is zero</exception>
    public IPAddress LastHost
    {
        get
        {
            // special cases
            // /0's are network addresses, they have no host addresses
            // /31's and /127 have not network or broadcast address and are all host addresses
            // /32's and /128's are host addresses. Host addresses are the first and last address
            if (Length == 0)
                throw new ArgumentOutOfRangeException();

            if (Length == AddressLength)
                return Address;

            if (Length == AddressLength - 1)
                return UInt128ToIpAddress(NetworkBits ^ HostMaskBits);

            return UInt128ToIpAddress(NetworkBits ^ (HostMaskBits - 1)); // BUG: is this correct?
        }
    }

    /// <summary>
    ///     The network mask
    /// </summary>
    public IPAddress Mask
    {
        get
        {
            return Version switch
            {
                IPVersion.IPv4 => Ipv4NetworkMaskObjects[Length],
                IPVersion.IPv6 => Ipv6NetworkMaskObjects[Length],
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    /// <summary>
    ///     The total count of addresses in the <c>NetworkPrefix</c>
    /// </summary>
    public BigInteger TotalAddresses
    {
        get
        {
            return Version switch
            {
                IPVersion.IPv4 => Ipv4AddressCount[Length],
                IPVersion.IPv6 => Ipv6AddressCount[Length],
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    /// <summary>
    ///     The total number of host address in the <c>NetworkPrefix</c>
    /// </summary>
    public UInt128 TotalHosts
    {
        get
        {
            return Version switch
            {
                IPVersion.IPv4 => Ipv4HostCount[Length],
                IPVersion.IPv6 => Ipv6HostCount[Length],
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    /// <summary>
    ///     The wildcard mask of the <c>NetworkPrefix</c>.
    ///     A wildcard mask is the inverted subnet mask
    /// </summary>
    public IPAddress Wildcard
    {
        get
        {
            return Version switch
            {
                IPVersion.IPv4 => Ipv4HostMaskObjects[Length],
                IPVersion.IPv6 => Ipv6HostMaskObjects[Length],
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    /// <summary>
    ///     The network address
    /// </summary>
    public IPAddress Address { get; set; }

    /// <summary>
    ///     The length of the prefix that is used to represent the <c>NetworkPrefix</c> portion of the address
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    ///     The version of Internet Protocol represented by the <c>NetworkPrefix</c>
    /// </summary>
    public IPVersion Version { get; set; }

    public UInt128 HostMaskBits { get; set; }

    public UInt128 NetworkMaskBits { get; set; }

    static NetworkPrefix()
    {
        // compute the bit values for all possible masks
        for (var i = 31; i >= 0; i--)
            Ipv4HostMaskBits[i] = (Ipv4HostMaskBits[i + 1] << 1) ^ 1;

        for (var i = 127; i >= 0; i--)
            Ipv6HostMaskBits[i] = (Ipv6HostMaskBits[i + 1] << 1) ^ 1;

        Ipv4NetworkMaskBits[32] = 0xff_ff_ff_ff;
        for (var i = 31; i >= 0; i--)
            Ipv4NetworkMaskBits[i] = (Ipv4NetworkMaskBits[i + 1] << 1) & Ipv4NetworkMaskBits[32];

        Ipv6NetworkMaskBits[128] = new UInt128(0xff_ff_ff_ff_ff_ff_ff_ff, 0xff_ff_ff_ff_ff_ff_ff_ff);
        for (var i = 127; i >= 0; i--)
            Ipv6NetworkMaskBits[i] = (Ipv6NetworkMaskBits[i + 1] << 1) & Ipv6NetworkMaskBits[128];

        // compute the IP address objects for all possible masks
        for (var i = 0; i < 33; i++)
        {
            var addressBytes =
                ((BigInteger)Ipv4HostMaskBits[i])
                .ToByteArray()
                .Concat(Enumerable.Repeat<byte>(0, 4))
                .Take(4)
                .Reverse()
                .ToArray();

            Ipv4HostMaskObjects[i] = new IPAddress(addressBytes);
        }

        for (var i = 0; i < 129; i++)
        {
            var addressBytes =
                ((BigInteger)Ipv6HostMaskBits[i])
                .ToByteArray()
                .Concat(Enumerable.Repeat<byte>(0, 16))
                .Take(16)
                .Reverse()
                .ToArray();

            Ipv6HostMaskObjects[i] = new IPAddress(addressBytes);
        }

        for (var i = 0; i < 33; i++)
        {
            var addressBytes =
                ((BigInteger)Ipv4NetworkMaskBits[i])
                .ToByteArray()
                .Concat(Enumerable.Repeat<byte>(0, 4))
                .Take(4)
                .Reverse()
                .ToArray();

            Ipv4NetworkMaskObjects[i] = new IPAddress(addressBytes);
        }

        for (var i = 0; i < 129; i++)
        {
            var addressBytes =
                ((BigInteger)Ipv6NetworkMaskBits[i])
                .ToByteArray()
                .Concat(Enumerable.Repeat<byte>(0, 16))
                .Take(16)
                .Reverse()
                .ToArray();

            Ipv6NetworkMaskObjects[i] = new IPAddress(addressBytes);
        }

        // generate a dictionary of the prefix length for each possible prefix
        for (var i = 0; i < 32; i++)
            IpHostMaskToPrefix.Add(Ipv4HostMaskObjects[i], i);

        for (var i = 0; i < 128; i++)
            IpHostMaskToPrefix.Add(Ipv6HostMaskObjects[i], i);

        for (var i = 0; i < 33; i++)
            IpNetworkMaskToPrefix.Add(Ipv4NetworkMaskObjects[i], i);

        for (var i = 0; i < 129; i++)
            IpNetworkMaskToPrefix.Add(Ipv6NetworkMaskObjects[i], i);

        // calculate the total hosts per network
        UInt128 count = 0xff_ff_ff_ff;
        for (var i = 0; i < 31; i++)
        {
            count = count >> 1;
            Ipv4HostCount[i] = count << 1;
        }

        Ipv4HostCount[31] = 2;
        Ipv4HostCount[32] = 1;

        count = new UInt128(0xff_ff_ff_ff_ff_ff_ff_ff, 0xff_ff_ff_ff_ff_ff_ff_ff);
        for (var i = 0; i < 127; i++)
        {
            count = count >> 1;
            Ipv6HostCount[i] = count << 1;
        }

        Ipv6HostCount[127] = 2;
        Ipv6HostCount[128] = 1;

        // calculate the total addresses per network
        for (var i = 0; i < 32; i++)
            Ipv4AddressCount[i] = BigInteger.One << (32 - i);

        Ipv4AddressCount[32] = 1;


        for (var i = 0; i < 128; i++)
            Ipv6AddressCount[i] = BigInteger.One << (128 - i);

        Ipv6AddressCount[128] = 1;

        // populate the allocation lookup
        Allocations = new Dictionary<NetworkPrefix, AddressAllocation>
        {
            {"::/0", AddressAllocation.Undefined},
            { "0.0.0.0/0", AddressAllocation.Undefined },

            { "10.0.0.0/8", AddressAllocation.Private },
            { "172.16.0.0/12", AddressAllocation.Private },
            { "192.168.0.0/16", AddressAllocation.Private },

            { "0.0.0.0/5", AddressAllocation.Global },
            { "8.0.0.0/7", AddressAllocation.Global },
            { "11.0.0.0/8", AddressAllocation.Global },
            { "12.0.0.0/6", AddressAllocation.Global },
            { "16.0.0.0/4", AddressAllocation.Global },
            { "32.0.0.0/3", AddressAllocation.Global },
            { "64.0.0.0/2", AddressAllocation.Global },
            { "128.0.0.0/3", AddressAllocation.Global },
            { "160.0.0.0/5", AddressAllocation.Global },
            { "168.0.0.0/6", AddressAllocation.Global },
            { "172.0.0.0/12", AddressAllocation.Global },
            { "172.32.0.0/11", AddressAllocation.Global },
            { "172.64.0.0/10", AddressAllocation.Global },
            { "172.128.0.0/9", AddressAllocation.Global },
            { "173.0.0.0/8", AddressAllocation.Global },
            { "174.0.0.0/7", AddressAllocation.Global },
            { "176.0.0.0/4", AddressAllocation.Global },
            { "192.0.0.0/9", AddressAllocation.Global },
            { "192.128.0.0/11", AddressAllocation.Global },
            { "192.160.0.0/13", AddressAllocation.Global },
            { "192.169.0.0/16", AddressAllocation.Global },
            { "192.170.0.0/15", AddressAllocation.Global },
            { "192.172.0.0/14", AddressAllocation.Global },
            { "192.176.0.0/12", AddressAllocation.Global },
            { "192.192.0.0/10", AddressAllocation.Global },
            { "193.0.0.0/8", AddressAllocation.Global },
            { "194.0.0.0/7", AddressAllocation.Global },
            { "196.0.0.0/6", AddressAllocation.Global },
            { "200.0.0.0/5", AddressAllocation.Global },
            { "208.0.0.0/4", AddressAllocation.Global },

            { "169.254.0.0/16", AddressAllocation.AutomaticPrivateIPAddressing },
            { "127.0.0.0/8", AddressAllocation.Loopback },
            { "0.0.0.0/8", AddressAllocation.CurrentNetwork },
            { "240.0.0.0/4", AddressAllocation.Experimental },
            { "224.0.0.0/4", AddressAllocation.Multicast },
            { "255.255.255.255/32", AddressAllocation.Broadcast }
        };

        // populate the class lookup
        Classes = new Dictionary<NetworkPrefix, NetworkClass>
        {
            { "::/0", NetworkClass.Undefined },
            { "0.0.0.0/0", NetworkClass.Undefined },
            { "0.0.0.0/1", NetworkClass.A },
            { "128.0.0.0/2", NetworkClass.B },
            { "192.0.0.0/3", NetworkClass.C },
            { "224.0.0.0/4", NetworkClass.D },
            { "240.0.0.0/4", NetworkClass.E }
        };
    }

    private NetworkPrefix()
    {
    }

    public NetworkPrefix(IPAddress ipAddress, int prefix)
    {
        SetNetworkValues(ipAddress, prefix);
    }


    public NetworkPrefix(IPAddress ipAddress) : this(ipAddress, GetHostAddressPrefix(ipAddress))
    {
    }


    public NetworkPrefix(IPAddress ipAddress, IPAddress mask) : this(ipAddress, GetPrefix(mask))
    {
    }

    public IEnumerable<IPAddress> Addresses()
    {
        var startAddress = NetworkBits;
        var endAddress = NetworkBits ^ HostMaskBits;

        for (var i = startAddress; i <= endAddress; i++)
            yield return UInt128ToIpAddress(i);
    }

    public bool ContainedByOrEqual(NetworkPrefix networkPrefix)
    {
        return networkPrefix.ContainsOrEqual(this);
    }

    public bool Contains(IPAddress address)
    {
        var hostNetwork = new NetworkPrefix(address);

        return Contains(hostNetwork);
    }

    /// <summary>
    ///     Determines if the
    ///     <param name="networkPrefix"></param>
    ///     is contained within this network
    /// </summary>
    /// <param name="networkPrefix"></param>
    /// <returns></returns>
    public bool Contains(NetworkPrefix networkPrefix)
    {
        // BUG: should we throw an exception for this? hmm...
        if (Version != networkPrefix.Version)
            return false;

        // a small network can't contain a bigger one...
        if (Length >= networkPrefix.Length)
            return false;

        // the smaller network must have the same network prefix as the larger network

        // AND the network mask of this address to the network bits of the other network
        // if the network is contained by this network then the resulting network bits should be the same as this networks bits.
        return (networkPrefix.NetworkBits & NetworkMaskBits) == NetworkBits;
    }

    public bool ContainsBy(NetworkPrefix networkPrefix)
    {
        return networkPrefix.Contains(this);
    }

    /// <summary>
    ///     Determines if the <paramref name="networkPrefix"></paramref> is contained within this <c>NetworkPrefix</c> or equal to it
    /// </summary>
    /// <param name="networkPrefix"></param>
    /// <returns></returns>
    public bool ContainsOrEqual(NetworkPrefix networkPrefix)
    {
        // BUG: should we throw an exception for this? hmm...
        if (Version != networkPrefix.Version)
            return false;

        // a small network can't contain a bigger one...
        if (Length > networkPrefix.Length)
            return false;

        // the smaller network must have the same network prefix as the larger network

        // AND the network mask of this address to the network bits of the other network
        // if the network is contained by this network then the resulting network bits should be the same as this networks bits.
        return (networkPrefix.NetworkBits & NetworkMaskBits) == NetworkBits;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
            return false;

        if (GetType() != obj.GetType())
            return false;

        var network = (NetworkPrefix)obj;
        return network.Address.Equals(Address) && network.Length == Length;
    }


    /// <summary>
    ///     Returns a new <c>NetworkPrefix</c> that covers both this <c>NetworkPrefix</c> and the <paramref name="networkPrefix" />
    /// </summary>
    /// <param name="networkPrefix"></param>
    /// <returns></returns>
    public NetworkPrefix GetContainingNetwork(NetworkPrefix networkPrefix)
    {
        return GetContainingNetwork(this, networkPrefix);
    }


    /// <summary>
    ///     Computes the smallest <c>NetworkPrefix</c> that contains both <c>NetworkPrefix</c>
    /// </summary>
    /// <param name="network1"></param>
    /// <param name="network2"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Thrown if the networks are not from the same address family</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NetworkPrefix GetContainingNetwork(NetworkPrefix network1, NetworkPrefix network2)
    {
        // ensure the network types are the same
        if (network1.Version != network2.Version)
            throw new ArgumentException("Networks must be from the same address family");

        // special case: if either network has a prefix of 0, then the only network that can contain it is 0.0.0.0/0
        if (network1.Length == 0 || network2.Length == 0)
            return new NetworkPrefix(IPAddress.None, 0);

        // figure out the most specific prefix that includes both networks

        // first XOR the two networks together
        // the parts that are different will be set to '1', for example
        // 10.20.30.1 00001010.00010100.00011110.00000001
        // 10.20.30.2 00001010.00010100.00011110.00000010
        //            00000000.00000000.00000000.00000011
        var mostSignificantBit = network1.NetworkBits ^ network2.NetworkBits;

        // if there are no significant bits then the network is completely contained by the network with the smaller prefix
        if (mostSignificantBit == 0)
            return network1.Length < network2.Length ? network1 : network2;

        // in the above example the split needs to occur at prefix 31
        // which is the most specific set bit
        // the position of the set bit dictates the new prefix

        // start checking for set bits at the larger of the smaller of the two networks prefix
        var bitPosition = 0; //network1.Prefix > network2.Prefix ? network1.Prefix : network2.Prefix;
        while (mostSignificantBit >> bitPosition > UInt128.One)
            bitPosition++;

        var newPrefix = network2.AddressLength - bitPosition - 1;

        return new NetworkPrefix(network1.Address, newPrefix);
    }

    /// <summary>
    ///     Computes the smallest <c>NetworkPrefix</c> that contains all of the <c>NetworkPrefix</c>. The resulting <c>NetworkPrefix</c> may contain additional
    ///     address space.
    /// </summary>
    /// <param name="ipNetworks">List of networks</param>
    /// <returns>The network which contains all of the supplied networks.</returns>
    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static NetworkPrefix GetContainingNetwork(IEnumerable<NetworkPrefix> ipNetworks)
    {
        NetworkPrefix? containingNetwork = null;
        foreach (var ipNetwork in ipNetworks)
        {
            if (containingNetwork == null)
            {
                containingNetwork = ipNetwork;
                continue;
            }

            containingNetwork = GetContainingNetwork(containingNetwork, ipNetwork);
        }

        if (containingNetwork == null)
            throw new ArgumentNullException();

        return containingNetwork;
    }


    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        hashCode.Add(Length);
        hashCode.AddBytes(Address.GetAddressBytes());

        return hashCode.ToHashCode();
    }

    /// <summary>
    ///     Returns an enumerator for all host addresses
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IPAddress> Hosts()
    {
        UInt128 startAddress;
        UInt128 endAddress;

        if (Length == AddressLength) // /32 and /128 are host addresses
        {
            startAddress = NetworkBits;
            endAddress = NetworkBits;
        }
        else if (Length == AddressLength - 1) // /31 and /127 are point to point addresses
        {
            startAddress = NetworkBits;
            endAddress = NetworkBits + UInt128.One;
        }
        else
        {
            startAddress = NetworkBits + 1;
            endAddress = NetworkBits ^ (HostMaskBits - 1); // BUG: is this correct?
        }

        for (var i = startAddress; i <= endAddress; i++)
            yield return UInt128ToIpAddress(i);
    }

    /// <summary>
    ///     Increments this <c>NetworkPrefix</c>
    ///     <param name="valueToAdd" />
    ///     to the
    /// </summary>
    /// <param name="networkPrefix">The <c>NetworkPrefix</c> to add to</param>
    /// <param name="valueToAdd">The number of <c>NetworkPrefix</c> to add</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when it is not possible to add the <paramref name="valueToAdd" />
    ///     to the <paramref name="networkPrefix" />
    /// </exception>
    public static NetworkPrefix operator +(NetworkPrefix networkPrefix, UInt128 valueToAdd)
    {
        try
        {
            checked
            {
                var addressesPerNetwork = UInt128.One << (networkPrefix.AddressLength - networkPrefix.Length);
                var addressesToAdd = addressesPerNetwork * valueToAdd;
                var networkBits = networkPrefix.NetworkBits + addressesToAdd;

                if (networkPrefix.Version == IPVersion.IPv4 && networkBits > Ipv4NetworkMaskBits[networkPrefix.Length])
                    throw new ArgumentOutOfRangeException();

                if (networkPrefix.Version == IPVersion.IPv6 && networkBits > Ipv6NetworkMaskBits[networkPrefix.Length])
                    throw new ArgumentOutOfRangeException();

                var ipAddress = networkBits.ToIpAddress(networkPrefix.Version);
                var newNetwork = new NetworkPrefix(ipAddress, networkPrefix.Length);

                return newNetwork;
            }
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    public static bool operator ==(NetworkPrefix? network1, NetworkPrefix? network2)
    {
        if (ReferenceEquals(network1, null) && ReferenceEquals(network2, null))
            return true;

        if (ReferenceEquals(network1, null) || ReferenceEquals(network2, null))
            return false;

        return network1.Address.Equals(network2.Address) && network1.Length == network2.Length;
    }

    public static bool operator >(NetworkPrefix network1, NetworkPrefix network2)
    {
        // ipv6 is greater then ipv4
        if (network1.Version != network2.Version)
            return network2.Version == IPVersion.IPv4;

        // if both side have the same network bits, then the one with the biggest mask is greater 
        if (network1.NetworkBits == network2.NetworkBits)
            return network1.Length > network2.Length;

        // otherwise the one with the biggest network is biggest
        return network1.NetworkBits > network2.NetworkBits;
    }

    public static implicit operator NetworkPrefix(string value)
    {
        return Parse(value);
    }

    public static implicit operator string(NetworkPrefix value)
    {
        return value.ToString();
    }

    public static bool operator !=(NetworkPrefix network1, NetworkPrefix network2)
    {
        return !(network1 == network2);
    }

    public static bool operator <(NetworkPrefix network1, NetworkPrefix network2)
    {
        // ipv4 is less then ipv6
        if (network1.Version != network2.Version)
            return network1.Version == IPVersion.IPv4;

        // if both side have the same network bits, then the one with the smallest mask is smaller 
        if (network1.NetworkBits == network2.NetworkBits)
            return network1.Length < network2.Length;

        // otherwise the one with the smallest network is smallest
        return network1.NetworkBits < network2.NetworkBits;
    }

    public static NetworkPrefix operator -(NetworkPrefix networkPrefix, UInt128 valueToSubtract)
    {
        try
        {
            checked
            {
                var addressesPerNetwork = UInt128.One << (networkPrefix.AddressLength - networkPrefix.Length);
                var addressToSubtract = addressesPerNetwork * valueToSubtract;
                var networkBits = networkPrefix.NetworkBits - addressToSubtract;

                if (networkPrefix.Version == IPVersion.IPv4 && networkBits > Ipv4NetworkMaskBits[networkPrefix.Length])
                    throw new ArgumentOutOfRangeException();

                if (networkPrefix.Version == IPVersion.IPv6 && networkBits > Ipv6NetworkMaskBits[networkPrefix.Length])
                    throw new ArgumentOutOfRangeException();

                var ipAddress = networkBits.ToIpAddress(networkPrefix.Version);
                var newNetwork = new NetworkPrefix(ipAddress, networkPrefix.Length);

                return newNetwork;
            }
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    ///     Parses a string to a <c>NetworkPrefix</c> object
    /// </summary>
    /// <param name="ipNetworkString">
    ///     A string that represents a <c>NetworkPrefix</c> The following formats are supported:10.0.0.0/24, myhost/24, 10.0.0.10, 10.0.0.0 255.255.255.0, myhost.mydomain.org 255.255.255.0, myhost
    /// </param>
    /// <returns>The parsed <c>NetworkPrefix</c> object</returns>
    public static NetworkPrefix Parse(string ipNetworkString)
    {
        if (TryParse(ipNetworkString, out var network))
            return network;

        throw new FormatException("Address is malformed");
    }


    public List<NetworkPrefix> RemoveNetwork(NetworkPrefix networkPrefixToRemove)
    {
        if (networkPrefixToRemove.Contains(this))
            throw new ArgumentException("The network to remove contains the network");

        if (this == networkPrefixToRemove)
            throw new ArgumentException("The network to remove and the network are the same");

        if (!Contains(networkPrefixToRemove))
            throw new ArgumentException("The network to remove is not contained by this network");

        var results = RemoveNetwork(new List<NetworkPrefix>(new[] { this }), networkPrefixToRemove);
        return results;
    }

    /// <summary>
    ///     Splits this <c>NetworkPrefix</c> into two equal <c>NetworkPrefix</c> objects.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Insufficient number network bits to perform the split</exception>
    public IEnumerable<NetworkPrefix> Split()
    {
        return Split(Length + 1);
    }

    /// <summary>
    ///     Splits this <c>NetworkPrefix</c> into two equal <c>NetworkPrefix</c> objects.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Insufficient number network bits to perform the split</exception>
    public IEnumerable<NetworkPrefix> Split(int prefixLength)
    {
        if (prefixLength >= AddressLength)
            throw new InvalidOperationException($"The split target for {this} of {Address}/{prefixLength} is not valid");

        var sizeOfNewNetworks = UInt128.One << (AddressLength - prefixLength);
        var numberOfNewNetworks = UInt128.One << (prefixLength - Length);

        var currentNetworkBits = NetworkBits;
        for (UInt128 i = 0; i < numberOfNewNetworks; i++)
        {
            var networkAddress = UInt128ToIpAddress(currentNetworkBits);
            var network = new NetworkPrefix(networkAddress, prefixLength);

            yield return network;

            currentNetworkBits += sizeOfNewNetworks;
        }
    }

    /// <summary>
    ///     Summarizes a list of <c>NetworkPrefix</c> such that the returned <c>NetworkPrefix</c> represents a compact and equivalent representation of
    ///     the original <c>NetworkPrefix</c>.
    /// </summary>
    /// <param name="networks">List of networks to summarize</param>
    /// <returns>A summarized, compact, and equivalent form of the original <c>NetworkPrefix</c></returns>
    public static IEnumerable<NetworkPrefix> Summarize(IEnumerable<NetworkPrefix> networks)
    {
        // create a lookup dictionary by prefix
        var networksByPrefix = new Dictionary<int, Dictionary<IPAddress, NetworkPrefix>>();

        // create a new, empty, bucket for each possible prefix
        for (var i = 0; i <= 128; i++)
            networksByPrefix.Add(i, new Dictionary<IPAddress, NetworkPrefix>());

        // add each network to the lookup by prefix
        foreach (var network in networks)
            networksByPrefix[network.Length].Add(network.Address, network);

        // go through each possible prefix, starting with the largest prefix
        for (var prefixLength = 128; prefixLength > 0; prefixLength--)

            // iterate over each network at a given prefix
            foreach (var network in networksByPrefix[prefixLength].ToArray())
            {
                // network could have been previously removed
                // if the network no longer exists, skip this network
                if (!networksByPrefix[prefixLength].Contains(network))
                    continue;

                // check to see if the complementary network exists
                // if the complementary network does not exist, then this network cannot be summarized
                var complementaryNetwork = network.Value.ComplementaryPrefix;
                if (!networksByPrefix[prefixLength].TryGetValue(complementaryNetwork.Address, out var complementaryX))
                    continue;

                // both halves of the less specific network exist
                // merge the two more specific halves into the more specific version
                // by replacing the two more specific networks with a new, less specific network
                // BUG: this assumes that there is no overlap...
                var summarizedNetwork = new NetworkPrefix(network.Key, prefixLength - 1);

                // BUG: Index out of bounds?
                // add the new summarized network
                networksByPrefix[prefixLength - 1].Add(summarizedNetwork.Address, summarizedNetwork);

                // remove the two more specific haves of the summarized network that are now covered by the new summarized network
                networksByPrefix[prefixLength].Remove(network.Key);
                networksByPrefix[prefixLength].Remove(complementaryNetwork.Address);
            }

        // return each network in the network lookup table
        foreach (var ipNetworks in networksByPrefix.Values)
        foreach (var ipNetwork in ipNetworks)
            yield return ipNetwork.Value;
    }

    public override string ToString()
    {
        return ToString(NetworkFormat.AddressAndPrefix);
    }

    public string ToString(NetworkFormat format)
    {
        switch (format)
        {
            case NetworkFormat.AddressAndPrefix:
                return $"{Address}/{Length}";

            case NetworkFormat.AddressAndMask:
                return $"{Address} {Mask}";

            case NetworkFormat.Mask:
                return $"{Mask}";

            case NetworkFormat.Address:
                return $"{Address}";

            case NetworkFormat.Detail:
            {
                var sb = new StringBuilder();

                var networkProperty = new List<(string Field, string Value, string Bits)>();
                networkProperty.Add(new ValueTuple<string, string, string>("Address:", Address.ToString(), BitsToString(NetworkBits)));
                networkProperty.Add(new ValueTuple<string, string, string>("Mask:", Mask.ToString(), BitsToString(NetworkMaskBits)));
                networkProperty.Add(new ValueTuple<string, string, string>("Wildcard:", Wildcard.ToString(), BitsToString(Wildcard)));
                networkProperty.Add(new ValueTuple<string, string, string>("Broadcast:", Broadcast.ToString(), BitsToString(Broadcast)));

                if (Length > 0)
                {
                    networkProperty.Add(new ValueTuple<string, string, string>("First Host:", FirstHost.ToString(), BitsToString(FirstHost)));
                    networkProperty.Add(new ValueTuple<string, string, string>("Last Host:", LastHost.ToString(), BitsToString(LastHost)));
                }

                networkProperty.Add(new ValueTuple<string, string, string>("Hosts/Net:", TotalHosts.ToString("0,0", CultureInfo.InvariantCulture), ""));

                var column = new int[3];
                foreach (var (field, value, bits) in networkProperty)
                {
                    if (field.Length > column[0])
                        column[0] = field.Length;

                    if (value.Length > column[1])
                        column[1] = value.Length;

                    if (bits.Length > column[2])
                        column[2] = bits.Length;
                }

                foreach (var (field, value, bits) in networkProperty)
                    sb.AppendLine($"{field.PadRight(column[0])} {value.PadRight(column[1])}  {bits.PadRight(column[2])}".TrimEnd());

                return sb.ToString();
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    ///     Attempts to parse a string to a <c>NetworkPrefix</c> object
    /// </summary>
    /// <param name="ipNetworkString">
    ///     A string that represents a network. The following formats are supported:10.0.0.0/24, myhost/24, 10.0.0.10, 10.0.0.0
    ///     255.255.255.0, myhost.mydomain.org 255.255.255.0, myhost
    /// </param>
    /// <param name="network"></param>
    /// <returns>True if the network was successfully parsed, otherwise false</returns>
    public static bool TryParse(string ipNetworkString, [NotNullWhen(true)] out NetworkPrefix? network)
    {
        if (TryParse(ipNetworkString, out var networkAddress, out var addressLength))
        {
            network = new NetworkPrefix(networkAddress, addressLength);
            return true;
        }

        network = default;
        return false;
    }

    // should these helper functions be somewhere else
    private string BitsToString(IPAddress value)
    {
        return BitsToString(value.ToUInt128());
    }

    private string BitsToString(UInt128 value)
    {
        var sb = new StringBuilder();

        for (var i = AddressLength - 1; i >= 0; i--)
        {
            if ((value & (UInt128.One << i)) > UInt128.Zero)
                sb.Append('1');
            else
                sb.Append('0');

            if (Version == IPVersion.IPv4 && i % 8 == 0 && i > 0)
                sb.Append('.');
            else if (Version == IPVersion.IPv6 && i % 16 == 0 && i > 0)
                sb.Append(':'); // BUG: Does this work correctly with IPv6?
        }

        return sb.ToString();
    }

    private static int GetHostAddressPrefix(IPAddress ipAddress)
    {
        return ipAddress.AddressFamily switch
        {
            AddressFamily.InterNetwork => 32,
            AddressFamily.InterNetworkV6 => 128,
            _ => -1
        };
    }

    private static int GetPrefix(IPAddress mask)
    {
        if (IpNetworkMaskToPrefix.TryGetValue(mask, out var maskLength))
            return maskLength;

        throw new ArgumentOutOfRangeException(nameof(mask), $"Invalid subnet mask: {mask}");
    }

    private static List<NetworkPrefix> RemoveNetwork(List<NetworkPrefix> listOfNetworks, NetworkPrefix networkPrefixToRemove)
    {
        // TODO: Handle if the networktoremove overlaps a network on the listofnetworks.
        // TODO: could handle it by disallowing overlapping networks
        // BUG: If you try to remove a network that is not contained, but has the same mask, you get a stack overflow (172.16.0.0/16 and 172.1.0.0/16 for example...

        if (listOfNetworks.Contains(networkPrefixToRemove))
        {
            listOfNetworks.Remove(networkPrefixToRemove);
            return listOfNetworks;
        }

        foreach (var network in listOfNetworks)
        {
            if (!network.Contains(networkPrefixToRemove))
                continue;

            listOfNetworks.AddRange(network.Split());
            listOfNetworks.Remove(network);
            break;
        }

        return RemoveNetwork(listOfNetworks, networkPrefixToRemove);
    }

    private void SetNetworkValues(IPAddress ipAddress, int prefix)
    {
        if (ipAddress.AddressFamily != AddressFamily.InterNetwork && ipAddress.AddressFamily != AddressFamily.InterNetworkV6)
            throw new NotSupportedException($"Non-IPv4 or IPv6 addresses are not supported. The specified address family is {ipAddress.AddressFamily}");

        if (ipAddress.AddressFamily == AddressFamily.InterNetwork && prefix > 32)
            throw new ArgumentOutOfRangeException(nameof(prefix), "Maximum mask length of a IPv4 address is 32");

        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6 && prefix > 128)
            throw new ArgumentOutOfRangeException(nameof(prefix), "Maximum mask length of a IPv6 address is 128");

        Address = ipAddress;
        Length = prefix;

        Version = ipAddress.AddressFamily switch
        {
            AddressFamily.InterNetwork => IPVersion.IPv4,
            AddressFamily.InterNetworkV6 => IPVersion.IPv6,
            _ => throw new ArgumentOutOfRangeException()
        };

        NetworkMaskBits = Version switch
        {
            IPVersion.IPv4 => Ipv4NetworkMaskBits[Length],
            IPVersion.IPv6 => Ipv6NetworkMaskBits[Length],
            _ => throw new ArgumentOutOfRangeException()
        };

        HostMaskBits = Version switch
        {
            IPVersion.IPv4 => Ipv4HostMaskBits[Length],
            IPVersion.IPv6 => Ipv6HostMaskBits[Length],
            _ => throw new ArgumentOutOfRangeException()
        };

        // truncate the network address
        // for example 10.1.2.3/8 becomes 10.0.0.0/8
        NetworkBits = ipAddress.ToUInt128() & NetworkMaskBits;
        Address = UInt128ToIpAddress(NetworkBits);

        AddressLength = Version switch
        {
            IPVersion.IPv4 => 32,
            IPVersion.IPv6 => 128,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static bool TryParse(string ipNetworkString, out IPAddress? networkAddress, out ushort addressLength)
    {
        networkAddress = default;
        addressLength = default;

        var networkParts = ipNetworkString.Split(' ', '/');

        if (networkParts.Length is 0 or > 3)
            return false;

        if (!IPAddress.TryParse(networkParts[0], out var networkPart))
        {
            // BUG: this can throw if it fails to resolve the name...
            IPAddress[] hostAddresses;

            try
            {
                // Dns.GetHostAddresses will throw on failure and there is no TryGetHostAddresses version
                hostAddresses = Dns.GetHostAddresses(networkParts[0]);
                if (hostAddresses.Length == 0)
                    return false;
            }
            catch
            {
                return false;
            }

            networkPart = hostAddresses[0];
        }

        var prefixLength = 0;
        IPAddress? maskAddress = null;

        switch (networkParts.Length)
        {
            case 1:
                switch (networkPart.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        prefixLength = 32;
                        break;
                    case AddressFamily.InterNetworkV6:
                        prefixLength = 128;
                        break;
                    default:
                        return false;
                }

                break;
            case 2:
                if (!int.TryParse(networkParts[1], out prefixLength))
                    if (!IPAddress.TryParse(networkParts[1], out maskAddress))
                        return false;

                break;
        }

        networkAddress = networkPart;
        if (maskAddress != null)
            addressLength = (ushort)GetPrefix(maskAddress);
        else
            addressLength = (ushort)prefixLength;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPAddress UInt128ToIpAddress(UInt128 addressBits)
    {
        return addressBits.ToIpAddress(Version);
    }

    public int CompareTo(NetworkPrefix? other)
    {
        if (ReferenceEquals(other, null))
            return -1;

        if (other == this)
            return 0;

        if (other > this)
            return -1;

        return 1;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public XmlSchema? GetSchema()
    {
        return null;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ReadXml(XmlReader reader)
    {
        reader.MoveToContent();

        if (reader.IsEmptyElement)
            throw new NullReferenceException();

        reader.ReadStartElement();
        var macAddressText = reader.ReadString();

        if (TryParse(macAddressText, out var networkAddress, out var prefix))
            SetNetworkValues(networkAddress, prefix);
        else
            throw new FormatException("Address is malformed");

        reader.ReadEndElement();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void WriteXml(XmlWriter writer)
    {
        writer.WriteString(ToString());
    }
}