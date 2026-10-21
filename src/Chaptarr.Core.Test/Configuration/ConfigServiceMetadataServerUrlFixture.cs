using NzbDrone.Core.Configuration;
using NUnit.Framework;

namespace NzbDrone.Core.Test.Configuration
{
    [TestFixture]
    public class ConfigServiceMetadataServerUrlFixture
    {
        [Test]
        public void saved_chaptarr_metadata_url_should_take_precedence_over_legacy_environment_value()
        {
            var url = ConfigService.ResolveMetadataServerUrl(
                "https://metadata.chaptarr.example",
                "https://metadata.bookshelf.example");

            Assert.That(url, Is.EqualTo("https://metadata.chaptarr.example"));
        }

        [Test]
        public void legacy_metadata_url_should_be_used_when_chaptarr_has_no_saved_value()
        {
            var url = ConfigService.ResolveMetadataServerUrl(
                string.Empty,
                "https://metadata.bookshelf.example");

            Assert.That(url, Is.EqualTo("https://metadata.bookshelf.example"));
        }

        [Test]
        public void metadata_server_should_keep_the_chaptarr_default_when_no_legacy_value_exists()
        {
            var url = ConfigService.ResolveMetadataServerUrl(string.Empty, null);

            Assert.That(url, Is.EqualTo("https://api2.chaptarr.com"));
        }
    }
}
