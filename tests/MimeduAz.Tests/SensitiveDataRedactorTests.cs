using FluentAssertions;
using MimeduAz.Application.Common.Utilities;

namespace MimeduAz.Tests;

public sealed class SensitiveDataRedactorTests
{
    private const int MaxLength = 4000;

    [Fact]
    public void Password_is_never_written_to_the_log()
    {
        var body = """{"email":"nigar@mimedu.az","password":"Teacher123!"}""";

        var redacted = SensitiveDataRedactor.Redact(body, MaxLength);

        redacted.Should().NotContain("Teacher123!");
        redacted.Should().Contain("nigar@mimedu.az");
        redacted.Should().Contain(SensitiveDataRedactor.Mask);
    }

    [Fact]
    public void Access_and_refresh_tokens_are_masked()
    {
        var body = """
        {"accessToken":"eyJhbGciOiJIUzI1NiJ9.payload.signature","refreshToken":"aVeryLongRandomValue=="}
        """;

        var redacted = SensitiveDataRedactor.Redact(body, MaxLength);

        redacted.Should().NotContain("eyJhbGciOiJIUzI1NiJ9");
        redacted.Should().NotContain("aVeryLongRandomValue");
    }

    [Fact]
    public void Token_expiry_timestamps_are_kept_readable()
    {
        var body = """{"accessToken":"secret-value","accessTokenExpiresAt":"2026-09-06T18:00:00Z"}""";

        var redacted = SensitiveDataRedactor.Redact(body, MaxLength);

        redacted.Should().NotContain("secret-value");
        // Vaxt damğası həssas deyil - debug üçün oxunaqlı qalmalıdır.
        redacted.Should().Contain("2026-09-06T18:00:00Z");
    }

    [Fact]
    public void Nested_objects_are_redacted()
    {
        var body = """{"user":{"email":"a@b.az","credentials":{"password":"gizli"}}}""";

        var redacted = SensitiveDataRedactor.Redact(body, MaxLength);

        redacted.Should().NotContain("gizli");
        redacted.Should().Contain("a@b.az");
    }

    [Fact]
    public void Arrays_of_objects_are_redacted()
    {
        var body = """[{"password":"bir"},{"password":"iki"}]""";

        var redacted = SensitiveDataRedactor.Redact(body, MaxLength);

        redacted.Should().NotContain("bir");
        redacted.Should().NotContain("iki");
    }

    [Theory]
    [InlineData("cardNumber")]
    [InlineData("cvc")]
    [InlineData("apiKey")]
    [InlineData("Authorization")]
    [InlineData("secretKey")]
    public void Known_sensitive_field_names_are_masked(string fieldName)
    {
        var body = $$"""{"{{fieldName}}":"həssas-dəyər"}""";

        var redacted = SensitiveDataRedactor.Redact(body, MaxLength);

        redacted.Should().NotContain("həssas-dəyər");
    }

    [Fact]
    public void Non_json_bodies_are_dropped_entirely()
    {
        // Format tanınmırsa şifrənin içində olmadığına zəmanət yoxdur - məzmun saxlanılmır.
        var redacted = SensitiveDataRedactor.Redact("email=a@b.az&password=gizli", MaxLength);

        redacted.Should().NotContain("gizli");
        redacted.Should().Contain("loglanmadı");
    }

    [Fact]
    public void Empty_body_returns_null()
    {
        SensitiveDataRedactor.Redact(null, MaxLength).Should().BeNull();
        SensitiveDataRedactor.Redact("   ", MaxLength).Should().BeNull();
    }

    [Fact]
    public void Long_bodies_are_truncated_with_a_marker()
    {
        var longText = new string('x', 500);
        var body = $$"""{"note":"{{longText}}"}""";

        var redacted = SensitiveDataRedactor.Redact(body, maxLength: 100);

        redacted!.Length.Should().BeLessThan(body.Length);
        redacted.Should().Contain("kəsildi");
    }

    [Fact]
    public void Ordinary_fields_survive_untouched()
    {
        var body = """{"name":"Kəsrlər üzrə iş vərəqi","grade":5,"price":6.00,"isPaid":true}""";

        var redacted = SensitiveDataRedactor.Redact(body, MaxLength);

        redacted.Should().Contain("Kəsrlər üzrə iş vərəqi");
        redacted.Should().Contain("5");
        redacted.Should().NotContain(SensitiveDataRedactor.Mask);
    }
}
