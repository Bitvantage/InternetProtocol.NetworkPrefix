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

using System.Net;

namespace Test;

public record NetworkPrefixTestRecord
{
    public IPAddress Address { get; init; }
    public IPAddress Broadcast { get; init; }
    public IPAddress? FirstHost { get; init; }
    public IPAddress? LastHost { get; init; }
    public IPAddress Mask { get; init; }
    public IPAddress Network { get; init; }
    public int Prefix { get; init; }
    public IPAddress Wildcard { get; init; }


    public NetworkPrefixTestRecord(string address, int prefix, string network, string mask, string broadcast, string wildcard, string firstHost, string lastHost)
    {
        Address = IPAddress.Parse(address);
        Network = IPAddress.Parse(network);
        Prefix = prefix;
        Mask = IPAddress.Parse(mask);
        Broadcast = IPAddress.Parse(broadcast);
        Wildcard = IPAddress.Parse(wildcard);
        FirstHost = IPAddress.Parse(firstHost);
        LastHost = IPAddress.Parse(lastHost);
    }
}