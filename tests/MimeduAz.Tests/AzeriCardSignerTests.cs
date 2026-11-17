using System.Security.Cryptography;
using FluentAssertions;
using MimeduAz.Infrastructure.Payments;

namespace MimeduAz.Tests;

public sealed class AzeriCardSignerTests
{
    [Fact]
    public void Sale_source_matches_documented_example()
    {
        // developer.azericard.com, bölmə 9: "5 11.48 3 USD 8 99999999 1 1 14 20030105153021 16 F2B2DD7E603A7ADA 14 www.sample.com"
        var fields = new Dictionary<string, string>
        {
            ["AMOUNT"] = "11.48",
            ["CURRENCY"] = "USD",
            ["TERMINAL"] = "99999999",
            ["TRTYPE"] = "1",
            ["TIMESTAMP"] = "20030105153021",
            ["NONCE"] = "F2B2DD7E603A7ADA",
            ["MERCH_URL"] = "www.sample.com"
        };

        AzeriCardSigner.BuildSource(fields, AzeriCardSigner.SaleFields)
            .Should().Be("511.483USD899999999111420030105153021" + "16F2B2DD7E603A7ADA14www.sample.com");
    }

    [Fact]
    public void Response_source_matches_documented_example_and_marks_missing_fields()
    {
        // Bölmə 9, callback: "5 11.48 8 99999999 6 168975 12 306276834930 16 4A29E93C607E33DC"
        var fields = new Dictionary<string, string>
        {
            ["AMOUNT"] = "11.48",
            ["TERMINAL"] = "99999999",
            ["APPROVAL"] = "168975",
            ["RRN"] = "306276834930",
            ["INT_REF"] = "4A29E93C607E33DC"
        };

        AzeriCardSigner.BuildSource(fields, AzeriCardSigner.ResponseFields)
            .Should().Be("511.488999999996168975" + "1230627683493016" + "4A29E93C607E33DC");

        fields.Remove("APPROVAL");
        AzeriCardSigner.BuildSource(fields, AzeriCardSigner.ResponseFields)
            .Should().Be("511.48899999999-12306276834930164A29E93C607E33DC");
    }

    [Fact]
    public void Status_source_matches_documented_example()
    {
        // Bölmə 8.3, TRTYPE=90: "14 20211112075614 8 17202191 2 90 14 20211112075714 16 7cfb4c2512eeec72"
        var fields = new Dictionary<string, string>
        {
            ["ORDER"] = "20211112075614",
            ["TERMINAL"] = "17202191",
            ["TRTYPE"] = "90",
            ["TIMESTAMP"] = "20211112075714",
            ["NONCE"] = "7cfb4c2512eeec72"
        };

        AzeriCardSigner.BuildSource(fields, AzeriCardSigner.StatusFields)
            .Should().Be("1420211112075614817202191290" + "1420211112075714167cfb4c2512eeec72");
    }

    [Fact]
    public void Signature_round_trips_and_detects_tampering()
    {
        using var key = RSA.Create(2048);
        const string source = "41.003AZN8172053561114202501230446081" + "63403fcef17df3c1c";

        var signature = AzeriCardSigner.Sign(source, key);

        signature.Should().MatchRegex("^[0-9a-f]{512}$");
        AzeriCardSigner.Verify(source, signature, key).Should().BeTrue();
        AzeriCardSigner.Verify(source, signature.ToUpperInvariant(), key).Should().BeTrue();
        AzeriCardSigner.Verify(source.Replace("1.00", "9.00"), signature, key).Should().BeFalse();
        AzeriCardSigner.Verify(source, "not-hex", key).Should().BeFalse();
    }

    [Fact]
    public void Pem_from_env_variable_with_escaped_newlines_is_loaded()
    {
        using var key = RSA.Create(2048);
        var pem = key.ExportRSAPrivateKeyPem().Replace("\n", "\\n");

        using var loaded = AzeriCardSigner.LoadKey(pem, null);

        AzeriCardSigner.Verify("x", AzeriCardSigner.Sign("x", key), loaded).Should().BeTrue();
    }

    [Theory]
    [InlineData("{\"ACTION\":\"0\",\"RC\":\"00\",\"ORDER\":\"123456\"}")]
    [InlineData("<response><ACTION>0</ACTION><RC>00</RC><ORDER>123456</ORDER></response>")]
    [InlineData("<html><form><input type=\"hidden\" name=\"ACTION\" value=\"0\"><input name=\"RC\" value=\"00\"><input name=\"ORDER\" value=\"123456\"></form></html>")]
    [InlineData("ACTION=0\nRC=00\nORDER=123456")]
    public void Gateway_responses_in_every_documented_format_are_parsed(string body)
    {
        var fields = AzeriCardGateway.ParseResponse(body);

        fields["ACTION"].Should().Be("0");
        fields["RC"].Should().Be("00");
        fields["ORDER"].Should().Be("123456");
    }

    [Fact]
    public void Amount_is_formatted_with_two_decimals_and_a_dot()
    {
        AzeriCardGateway.FormatAmount(25m).Should().Be("25.00");
        AzeriCardGateway.FormatAmount(8.5m).Should().Be("8.50");
    }
}
