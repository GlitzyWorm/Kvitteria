using System.Net;
using Backend.Features.Shared;

namespace Backend.Tests;

public class RequestProtectionTests
{
	[Fact]
	public void A_missing_address_shares_one_window()
	{
		Assert.Equal(RequestProtection.UnknownAddressKey, RequestProtection.PartitionKey(null));
	}

	[Fact]
	public void Different_addresses_get_different_windows()
	{
		Assert.NotEqual(
			RequestProtection.PartitionKey(IPAddress.Parse("203.0.113.7")),
			RequestProtection.PartitionKey(IPAddress.Parse("203.0.113.8")));
	}

	[Fact]
	public void An_ipv4_address_mapped_into_ipv6_is_the_same_client()
	{
		Assert.Equal(
			RequestProtection.PartitionKey(IPAddress.Parse("203.0.113.7")),
			RequestProtection.PartitionKey(IPAddress.Parse("::ffff:203.0.113.7")));
	}

	[Theory]
	[InlineData("10.1.2.3", true)]
	[InlineData("172.18.0.5", true)]
	[InlineData("172.32.0.1", false)]
	[InlineData("192.168.1.9", true)]
	[InlineData("fd12::1", true)]
	[InlineData("203.0.113.7", false)]
	[InlineData("2001:db8::1", false)]
	public void Only_private_peers_are_trusted_to_name_a_client(string peer, bool trusted)
	{
		IPAddress address = IPAddress.Parse(peer);
		Assert.Equal(trusted, RequestProtection.PrivateNetworks.Any(n => n.Contains(address)));
	}
}
