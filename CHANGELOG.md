# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - Unreleased

This release corrects four cases where a boundary condition produced the wrong answer, and renames one
method so that the containment API reads consistently. Every one of these changes can alter the result
of an existing call, so the major version has been incremented.

### Removed

- `ContainsBy(NetworkPrefix)` has been renamed to `ContainedBy(NetworkPrefix)`. The old name read as
  though it were a variant of `Contains`, when in fact it is the inverse of it and the strict
  counterpart to `ContainedByOrEqual`. There is no compatibility shim; callers must be updated.

  ```csharp
  // before
  smallerNetwork.ContainsBy(largerNetwork);

  // after
  smallerNetwork.ContainedBy(largerNetwork);
  ```

  The containment API is now two matched pairs, plus address membership:

  | Method                  | Meaning                                          |
  |-------------------------|--------------------------------------------------|
  | `Contains`              | strictly contains the other prefix                |
  | `ContainsOrEqual`       | contains the other prefix, or is equal to it      |
  | `ContainedBy`           | is strictly contained by the other prefix         |
  | `ContainedByOrEqual`    | is contained by the other prefix, or equal to it  |
  | `Contains(IPAddress)`   | the address is a member of this prefix            |

### Fixed

- `Contains(IPAddress)` returned `false` when a host prefix was asked about the single address it
  represents. An address is a member of a prefix rather than a prefix nested inside of it, so the
  overload now resolves through `ContainsOrEqual`. `Contains(NetworkPrefix)` is unchanged and remains
  strict; use `ContainsOrEqual` for the inclusive comparison between two prefixes.

  ```csharp
  NetworkPrefix.Parse("10.20.30.40/32").Contains(IPAddress.Parse("10.20.30.40")); // was false, now true
  ```

  Only `/32` and `/128` prefixes were affected; every shorter prefix already answered correctly.

- `GetContainingNetwork` derived the result from the differing bits of the two network addresses
  without consulting their prefix lengths. When one network contained the other but the two did not
  share the same network address, the differing bit fell inside the host portion of the less specific
  network and the result was too specific to cover it. The result is now never more specific than the
  less specific of the two networks. `GetContainingNetwork(IEnumerable<NetworkPrefix>)` is fixed by
  the same change.

  ```csharp
  NetworkPrefix.GetContainingNetwork("10.20.0.0/16", "10.20.30.0/24"); // was 10.20.0.0/19, now 10.20.0.0/16
  ```

- `Split(int)` rejected a host prefix as a split target, so a network could not be split into `/32` or
  `/128` prefixes and `Split()` on a `/31` or `/127` always threw. A host prefix is now a valid target.

  ```csharp
  NetworkPrefix.Parse("10.20.30.0/31").Split(); // was InvalidOperationException, now 10.20.30.0/32 and 10.20.30.1/32
  ```

- `RemoveNetwork(NetworkPrefix)` threw `InvalidOperationException` whenever the network being removed
  was a host prefix, because it splits down to the target and could not reach host length. Removing a
  host route from a network now works. This was a consequence of the `Split` defect above and needed
  no separate change.

  ```csharp
  NetworkPrefix.Parse("10.20.30.0/29").RemoveNetwork("10.20.30.1/32"); // was InvalidOperationException,
                                                                      // now 10.20.30.4/30, 10.20.30.2/31, 10.20.30.0/32
  ```

- `Split(int)` did not validate that the target was more specific than the network being split. A
  target equal to or shorter than the network produced a negative shift, which wrapped and yielded an
  effectively unbounded sequence of prefixes lying outside the network. Such a target now throws
  `InvalidOperationException`, consistent with a target longer than a host prefix.

  ```csharp
  NetworkPrefix.Parse("10.20.30.0/24").Split(16); // was 10.20.0.0/16, 10.21.0.0/16, ... without end,
                                                  // now InvalidOperationException
  ```

- `CompareTo(null)` returned `-1`, which violates the `IComparable<T>` contract that every instance
  compares greater than `null`. It now returns a positive value, so sorting a collection that contains
  nulls places them first. Collections sorted through `Comparer<T>.Default`, such as `OrderBy`, were
  never affected because that comparer handles nulls before delegating.

### Migration

- Replace calls to `ContainsBy` with `ContainedBy`.
- If you relied on `Contains(IPAddress)` returning `false` for a host prefix holding that address,
  compare the prefixes directly instead.
- If you relied on `Split` throwing for a target shorter than the network, that call now throws a
  different way round: it throws at enumeration rather than yielding wrong prefixes.

## [1.1.0] - 2024-09-16

### Changed

- The `Prefix` property was renamed to `Length`.

## [1.0.0] - 2024-06-28

- Initial release.

[2.0.0]: https://github.com/Bitvantage/InternetProtocol.NetworkPrefix/compare/v1.1.0...v2.0.0
[1.1.0]: https://github.com/Bitvantage/InternetProtocol.NetworkPrefix/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Bitvantage/InternetProtocol.NetworkPrefix/releases/tag/v1.0.0
