using FluentAssertions;
using ShopAPI.Application;
using ShopAPI.Application.Validators;
using Xunit;

namespace ShopAPI.Tests.Validators;

public class ValidatorTests
{
    [Fact]
    public void LoginRequestValidator_Should_Fail_When_Email_Invalid()
    {
        var validator = new LoginRequestValidator();
        var result = validator.Validate(new LoginRequest("bad-email", "password123"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LoginRequestValidator_Should_Pass_When_Valid()
    {
        var validator = new LoginRequestValidator();
        var result = validator.Validate(new LoginRequest("user@test.com", "password123"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ProductRequestValidator_Should_Fail_When_Price_Negative()
    {
        var validator = new ProductRequestValidator();
        var result = validator.Validate(new ProductRequest("Phone", 0, 10, Guid.NewGuid(), true));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RegisterRequestValidator_Should_Pass_When_Valid()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("Test User", "test@mail.com", "secret12"));
        result.IsValid.Should().BeTrue();
    }
}
