using System.Text.Json.Nodes;
using V2rayWatch.Server.Core;
using Xunit;

namespace V2rayWatch.Server.Core.Tests;

public sealed class Hysteria2ParserTests
{
    [Theory]
    [InlineData("hy2://secret-password@hy2.example.net:443?sni=cdn.example.net", "Hysteria2")]
    [InlineData("hysteria2://secret-password@hy2.example.net:443?sni=cdn.example.net", "Hysteria2")]
    public void Parses_Hysteria2_Uri_Into_Xray_V2_Outbound(string link, string protocol)
    {
        var profile = ServerProfileParser.ParseOne(link);
        Assert.Equal(protocol, profile.Protocol);
        Assert.Equal("hy2.example.net", profile.Host);
        Assert.Equal(443, profile.Port);

        var config = JsonNode.Parse(profile.ConfigJson)!.AsObject();
        var outbound = config["outbounds"]!.AsArray()[0]!.AsObject();
        Assert.Equal("hysteria", outbound["protocol"]!.GetValue<string>());
        Assert.Equal(2, outbound["settings"]!["version"]!.GetValue<int>());
        Assert.Equal("hy2.example.net", outbound["settings"]!["address"]!.GetValue<string>());
        var stream = outbound["streamSettings"]!.AsObject();
        Assert.Equal("hysteria", stream["method"]!.GetValue<string>());
        Assert.Equal(2, stream["hysteriaSettings"]!["version"]!.GetValue<int>());
        Assert.Equal("secret-password", stream["hysteriaSettings"]!["auth"]!.GetValue<string>());
        Assert.Equal("cdn.example.net", stream["tlsSettings"]!["serverName"]!.GetValue<string>());
    }

    [Fact]
    public void Parses_Hysteria2_Salamander_Obfuscation()
    {
        var profile = ServerProfileParser.ParseOne("hy2://password@hy2.example.net:443?obfs=salamander&obfs-password=obfs-secret");
        var config = JsonNode.Parse(profile.ConfigJson)!;
        var masks = config["outbounds"]![0]!["streamSettings"]!["finalmask"]!;
        Assert.Equal("salamander", masks["udp"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("obfs-secret", masks["udp"]![0]!["settings"]!["password"]!.GetValue<string>());
    }

    [Fact]
    public void Parses_Hysteria2_Port_Hopping_Ranges()
    {
        var profile = ServerProfileParser.ParseOne("hy2://password@hy2.example.net:20000-20010,443?mportInterval=20");
        var config = JsonNode.Parse(profile.ConfigJson)!;
        var hop = config["outbounds"]![0]!["streamSettings"]!["finalmask"]!["quicParams"]!["udpHop"]!;
        Assert.Equal("20000-20010,443", hop["ports"]!.GetValue<string>());
        Assert.Equal(20, hop["interval"]!.GetValue<int>());
    }

    [Fact]
    public void Parses_The_First_Link_From_Bulk_Text_And_Deduplicates()
    {
        const string link = "hy2://password@hy2.example.net:443";
        var profiles = ServerProfileParser.ParseMany(link + Environment.NewLine + link);
        Assert.Single(profiles);
        Assert.Equal("Hysteria2", profiles[0].Protocol);
    }

    [Theory]
    [InlineData("hy2://secret-password@hy2.example.net:443?sni=cdn.example.net", "hysteria2-basic.json")]
    [InlineData("hy2://secret-password@hy2.example.net:443?sni=cdn.example.net&obfs=salamander&obfs-password=obfs-secret", "hysteria2-salamander.json")]
    [InlineData("hy2://secret-password@hy2.example.net:20000-20010,443?sni=cdn.example.net&mportInterval=20", "hysteria2-port-hop.json")]
    public void Parser_Output_Matches_Xray_Core_Validation_Fixtures(string link, string fixture)
    {
        var profile = ServerProfileParser.ParseOne(link);
        var actual = JsonNode.Parse(profile.ConfigJson)!.AsObject();
        actual["inbounds"] = new JsonArray(new JsonObject
        {
            ["tag"] = "v2vw-socks",
            ["listen"] = "127.0.0.1",
            ["port"] = 10808,
            ["protocol"] = "socks",
            ["settings"] = new JsonObject { ["auth"] = "noauth", ["udp"] = false }
        });
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
        var expected = JsonNode.Parse(File.ReadAllText(expectedPath))!;
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Parser output differs from {fixture}");
    }

    [Fact]
    public void Rejects_Hysteria2_With_Invalid_Port()
    {
        Assert.Throws<FormatException>(() => ServerProfileParser.ParseOne("hy2://password@hy2.example.net:70000"));
    }
}
