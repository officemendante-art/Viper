using Xunit;
using Viper.Services;
using Viper.Models;

namespace Viper.Tests;

public class PasswordServiceTests
{
    [Fact]
    public void HashPassword_And_Verify_ShouldSucceedForCorrectPassword()
    {
        // Arrange
        string password = "my_secure_password123";

        // Act
        var result = PasswordService.HashPassword(password);
        bool isValid = PasswordService.Verify(password, result.Hash, result.Salt, result.Parameters);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ShouldFailForIncorrectPassword()
    {
        // Arrange
        var result = PasswordService.HashPassword("correct_password");

        // Act
        bool isValid = PasswordService.Verify("wrong_password", result.Hash, result.Salt, result.Parameters);

        // Assert
        Assert.False(isValid);
    }
}
