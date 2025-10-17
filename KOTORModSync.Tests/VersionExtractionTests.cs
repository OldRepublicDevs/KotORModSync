using Xunit;
using KOTORModSync.Dialogs;

namespace KOTORModSync.Tests
{
    public class VersionExtractionTests
    {
        [Xunit.Theory]
        [InlineData("v1.5.2-patcher", "1.5.2")]
        [InlineData("v1.5-patcher", "1.5")]
        [InlineData("V1.52-PATCHER", "1.52")]
        [InlineData("v2.0.1-patcher", "2.0.1")]
        [InlineData("1.5.2-patcher", "1.5.2")]
        [InlineData("v1.5.2-alpha-patcher", "1.5.2")]
        [InlineData("v1.5-beta-patcher", "1.5")]
        [InlineData("v1.5.2-rc1-patcher", "1.5.2")]
        [InlineData("v2.0-beta3-patcher", "2.0")]
        [InlineData("v1.5.2-dev-patcher", "1.5.2")]
        [InlineData("v1.5.2-anything-goes-patcher", "1.5.2")]
        public void ExtractVersionFromTag_ValidPatcherTags_ReturnsVersion(string tagName, string expectedVersion)
        {
            // Act
            string actualVersion = SettingsDialog.ExtractVersionFromTag(tagName);

            // Assert
            Xunit.Assert.Equal(expectedVersion, actualVersion);
        }

        [Xunit.Theory]
        [InlineData("invalid-tag")]
        [InlineData("v1.5.2")]
        [InlineData("v1.5.2-something-else")]
        [InlineData("")]
        public void ExtractVersionFromTag_InvalidTags_ReturnsNull(string tagName)
        {
            // Act
            string actualVersion = SettingsDialog.ExtractVersionFromTag(tagName);

            // Assert
            Xunit.Assert.Null(actualVersion);
        }

        [Xunit.Fact]
        public void ExtractVersionFromTag_NullInput_ReturnsNull()
        {
            // Act
            string actualVersion = SettingsDialog.ExtractVersionFromTag(null);

            // Assert
            Xunit.Assert.Null(actualVersion);
        }

        [Xunit.Fact]
        public void ExtractVersionFromTag_CaseInsensitive_Works()
        {
            // Arrange
            string lowerCase = "v1.5.2-patcher";
            string upperCase = "V1.5.2-PATCHER";
            string mixedCase = "v1.5.2-PATCHER";

            // Act
            string versionLower = SettingsDialog.ExtractVersionFromTag(lowerCase);
            string versionUpper = SettingsDialog.ExtractVersionFromTag(upperCase);
            string versionMixed = SettingsDialog.ExtractVersionFromTag(mixedCase);

            // Assert
            Xunit.Assert.Equal("1.5.2", versionLower);
            Xunit.Assert.Equal("1.5.2", versionUpper);
            Xunit.Assert.Equal("1.5.2", versionMixed);
        }

        [Xunit.Fact]
        public void ExtractVersionFromTag_WithoutVPrefix_Works()
        {
            // Arrange
            string withV = "v1.5.2-patcher";
            string withoutV = "1.5.2-patcher";

            // Act
            string versionWithV = SettingsDialog.ExtractVersionFromTag(withV);
            string versionWithoutV = SettingsDialog.ExtractVersionFromTag(withoutV);

            // Assert
            Xunit.Assert.Equal("1.5.2", versionWithV);
            Xunit.Assert.Equal("1.5.2", versionWithoutV);
        }

        [Xunit.Theory]
        [InlineData("v1.5.2-alpha-beta-gamma-patcher")]
        [InlineData("v1.5.2-rc1-final-patcher")]
        [InlineData("v1.5-anything-you-want-here-patcher")]
        public void ExtractVersionFromTag_ComplexMiddleText_ExtractsVersion(string tagName)
        {
            // Act
            string actualVersion = SettingsDialog.ExtractVersionFromTag(tagName);

            // Assert
            Xunit.Assert.NotNull(actualVersion);
            Xunit.Assert.Matches(@"^\d+\.\d+(\.\d+)?$", actualVersion);
        }
    }
}
