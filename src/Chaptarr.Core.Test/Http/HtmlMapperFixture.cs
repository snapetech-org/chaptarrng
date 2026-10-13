using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NUnit.Framework;
using Chaptarr.Http.Frontend.Mappers;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Core.Test.Http
{
    [TestFixture]
    public class HtmlMapperFixture
    {
        private sealed class AppFolderInfoStub : IAppFolderInfo
        {
            public string AppDataFolder => "/data";
            public string TempFolder => "/tmp";
            public string StartUpFolder => "/app";
        }

        private sealed class DiskProviderProxy : DispatchProxy
        {
            public string Html;

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                return targetMethod?.Name switch
                {
                    nameof(IDiskProvider.FileExists) => true,
                    nameof(IDiskProvider.ReadAllText) => Html,
                    _ => throw new NotImplementedException($"Test proxy does not implement IDiskProvider.{targetMethod?.Name}")
                };
            }
        }

        private sealed class ConfigFileProviderProxy : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                return targetMethod?.Name switch
                {
                    "get_UrlBase" => "/base",
                    "get_UiFolder" => "UI",
                    _ => throw new NotImplementedException($"Test proxy does not implement IConfigFileProvider.{targetMethod?.Name}")
                };
            }
        }

        private sealed class CacheBreakerStub : ICacheBreakerProvider
        {
            public string AddCacheBreakerToPath(string resourceUrl)
            {
                return resourceUrl + "?test=1";
            }
        }

        [TestCase("<link href=/static/css/main.css>", "href=\"/base/static/css/main.css?test=1\"")]
        [TestCase("<script src=/static/js/main.js?cb=123>", "src=\"/base/static/js/main.js?cb=123\"")]
        [TestCase("<link href='/static/css/main.css'>", "href=\"/base/static/css/main.css?test=1\"")]
        public void should_rewrite_quoted_and_unquoted_asset_urls_and_keep_existing_queries(string html, string expected)
        {
            var diskProvider = DispatchProxy.Create<IDiskProvider, DiskProviderProxy>();
            ((DiskProviderProxy)(object)diskProvider).Html = html;
            var mapper = new IndexHtmlMapper(
                new AppFolderInfoStub(),
                diskProvider,
                DispatchProxy.Create<IConfigFileProvider, ConfigFileProviderProxy>(),
                new Lazy<ICacheBreakerProvider>(() => new CacheBreakerStub()),
                LogManager.GetCurrentClassLogger());

            var response = mapper.GetResponse("/") as FileStreamResult;
            Assert.That(response, Is.Not.Null);

            using var reader = new StreamReader(response.FileStream);
            Assert.That(reader.ReadToEnd(), Does.Contain(expected));
        }
    }
}
