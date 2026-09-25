using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Validation.Storefront;
using Platform.Web.Common;
using Xunit;

namespace Platform.Tests.Unit;

/// <summary>
/// Phone numbers typed as a calling code plus a local number, joined into one
/// international number and checked.
/// </summary>
public sealed class PhoneNumberTests
{
    /// <summary>What people type is joined with the chosen code; spaces, dashes and a leading 0 go.</summary>
    /// <param name="code">Code chosen.</param>
    /// <param name="typed">Number typed.</param>
    /// <param name="expected">Stored number.</param>
    [Theory]
    [InlineData("+91", "98765 43210", "+919876543210")]
    [InlineData("+91", "098765-43210", "+919876543210")]
    [InlineData("+971", "(050) 123 4567", "+971501234567")]
    [InlineData("+91", "+971501234567", "+971501234567")]
    [InlineData("+999", "9876543210", "+919876543210")]
    [InlineData("+91", "   ", "")]
    public void JoinsCodeAndNumber(string code, string typed, string expected) =>
        Assert.Equal(expected, DialCodes.Join(code, typed));

    /// <summary>A stored number is split back into its code and local part for the form.</summary>
    [Fact]
    public void SplitsStoredNumbers()
    {
        Assert.Equal(("+91", "9876543210"), DialCodes.Split("+919876543210"));
        Assert.Equal(("+971", "501234567"), DialCodes.Split("+971501234567"));
        Assert.Equal(("+91", string.Empty), DialCodes.Split(null));
    }

    /// <summary>Indian mobiles need ten digits starting 6-9; other countries follow E.164.</summary>
    /// <param name="phone">Number.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("+919876543210", true)]
    [InlineData("+917015451189", true)]
    [InlineData("+915876543210", false)]
    [InlineData("+9198765432", false)]
    [InlineData("+971501234567", true)]
    [InlineData("9107015451189", false)]
    public void ChecksMobileNumbers(string phone, bool valid)
    {
        var result = new ShopRegisterRequestValidator().Validate(new ShopRegisterRequest
        {
            Name = "Test",
            Email = "test@example.com",
            Password = "Password1",
            Phone = phone,
        });

        Assert.Equal(valid, result.IsValid);
    }
}
