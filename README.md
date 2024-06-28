# Bitvantage.InternetProtocol.NetworkPrefix
A library for working with IPv4 and IPV6 network prefixes.

## Installing via NuGet Package Manager
```sh
PM> NuGet\Install-Package Bitvantage.InternetProtocol.NetworkPrefix
```

## Quick Start
```csharp
// create a new network prefix
var prefix = NetworkPrefix.Parse("10.0.0.0/24");

// get the total number of host addresses in a network
var totalHosts = prefix.TotalHosts;

// get the total number of addresses in a network
var totalAddresses = prefix.TotalAddresses;

// iterate through each host address
foreach(var address in prefix.Hosts()
	Console.WriteLine(address);

// determine if a prefix is contained by a different prefix
var prefixContainsPrefix = prefix.Contains("10.0.0.0/25");

// determine if a IP address is contained by a prefix
var prefixContainsHost = prefix.Contains(IPAddress.Parse("10.0.0.100");
```