using System;
using System.IO;
using Xunit;

namespace Glyph.Core.Tests.Configuration
{
    public class ValidationTests
    {
        [Fact]
        public void Validate_ConfigFile_ShouldReturnTrue_WhenValid()
        {
            // Arrange
            var configFilePath = Path.Combine("config", "config.toml");
            var validator = new ConfigValidator();

            // Act
            var result = validator.Validate(configFilePath);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Validate_ConfigFile_ShouldReturnFalse_WhenInvalid()
        {
            // Arrange
            var invalidConfigFilePath = Path.Combine("config", "invalid_config.toml");
            var validator = new ConfigValidator();

            // Act
            var result = validator.Validate(invalidConfigFilePath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Validate_ConfigFile_ShouldThrowException_WhenFileNotFound()
        {
            // Arrange
            var nonExistentFilePath = "non_existent_file.toml";
            var validator = new ConfigValidator();

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => validator.Validate(nonExistentFilePath));
        }
    }
}