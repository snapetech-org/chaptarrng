using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NzbDrone.Core.Configuration;
using NUnit.Framework;

namespace NzbDrone.Core.Test.EnvironmentInfo
{
    [TestFixture]
    public class LegacyConfigurationCompatibilityFixture
    {
        [Test]
        public void should_map_legacy_readarr_and_audioarr_options_to_chaptarr()
        {
            var configuration = BuildConfiguration(new Dictionary<string, string>
            {
                ["Audioarr:Server:BindAddress"] = "192.0.2.40",
                ["Audioarr:Server:Port"] = "8788",
                ["Readarr:Server:Port"] = "8787",
                ["Readarr:Auth:Method"] = "Forms",
                ["Readarr:App:Theme"] = "dark",
                ["Readarr:Log:Level"] = "debug",
                ["Readarr:Update:Branch"] = "nightly",
                ["Readarr:Postgres:Host"] = "database.internal"
            });

            var result = LegacyConfigurationCompatibility.AddFallbackSections(configuration);

            Assert.Multiple(() =>
            {
                Assert.That(result["Chaptarr:Server:BindAddress"], Is.EqualTo("192.0.2.40"));
                Assert.That(result["Chaptarr:Server:Port"], Is.EqualTo("8787"));
                Assert.That(result["Chaptarr:Auth:Method"], Is.EqualTo("Forms"));
                Assert.That(result["Chaptarr:App:Theme"], Is.EqualTo("dark"));
                Assert.That(result["Chaptarr:Log:Level"], Is.EqualTo("debug"));
                Assert.That(result["Chaptarr:Update:Branch"], Is.EqualTo("nightly"));
                Assert.That(result["Chaptarr:Postgres:Host"], Is.EqualTo("database.internal"));
            });
        }

        [Test]
        public void should_preserve_explicit_chaptarr_values_and_fill_only_missing_settings()
        {
            var configuration = BuildConfiguration(new Dictionary<string, string>
            {
                ["Audioarr:Server:Port"] = "8788",
                ["Readarr:Server:Port"] = "8787",
                ["Readarr:Server:BindAddress"] = "192.0.2.41",
                ["Readarr:Server:SslCertPassword"] = "legacy-value",
                ["Chaptarr:Server:Port"] = "9123",
                ["Chaptarr:Server:SslCertPassword"] = ""
            });

            Assert.That(configuration["Chaptarr:Server:SslCertPassword"], Is.Empty,
                "the in-memory fixture must represent an explicitly empty Chaptarr value");

            var result = LegacyConfigurationCompatibility.AddFallbackSections(configuration);
            Assert.That(result["Chaptarr:Server:SslCertPassword"], Is.Empty,
                "the explicit empty Chaptarr value must override the legacy value");

            Assert.Multiple(() =>
            {
                Assert.That(result["Chaptarr:Server:Port"], Is.EqualTo("9123"));
                Assert.That(result["Chaptarr:Server:BindAddress"], Is.EqualTo("192.0.2.41"));
            });
        }

        private static IConfiguration BuildConfiguration(IDictionary<string, string> values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }
    }
}
