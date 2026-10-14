using System;
using System.Reflection;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Update;

namespace Chaptarr.Core.Test.Configuration
{
    [TestFixture]
    public class DeploymentInfoProviderFixture
    {
        [Test]
        public void should_use_external_updates_for_native_package_bin_layout()
        {
            var packageInfoPath = "/usr/lib/chaptarrng/bin/../package_info";
            var diskProvider = CreateDiskProvider(packageInfoPath);
            var appFolderInfo = new TestAppFolderInfo("/usr/lib/chaptarrng/bin");

            var deploymentInfo = new DeploymentInfoProvider(appFolderInfo, diskProvider);

            Assert.Multiple(() =>
            {
                Assert.That(deploymentInfo.PackageUpdateMechanism, Is.EqualTo(UpdateMechanism.External));
                Assert.That(deploymentInfo.DefaultUpdateMechanism, Is.EqualTo(UpdateMechanism.External));
                Assert.That(deploymentInfo.IsExternalUpdateMechanism, Is.True);
                Assert.That(deploymentInfo.PackageBranch, Is.EqualTo("main"));
                Assert.That(deploymentInfo.PackageUpdateMechanismMessage, Is.EqualTo("Use your package manager to update ChaptarrNG."));
            });
        }

        [Test]
        public void should_not_load_package_info_outside_bin_startup_folder()
        {
            var packageInfoPath = "/usr/lib/chaptarrng/app/../package_info";
            var diskProvider = CreateDiskProvider(packageInfoPath);
            var appFolderInfo = new TestAppFolderInfo("/usr/lib/chaptarrng/app");

            var deploymentInfo = new DeploymentInfoProvider(appFolderInfo, diskProvider);

            Assert.That(deploymentInfo.PackageUpdateMechanism, Is.EqualTo(UpdateMechanism.BuiltIn));
            Assert.That(deploymentInfo.IsExternalUpdateMechanism, Is.False);
        }

        private static IDiskProvider CreateDiskProvider(string packageInfoPath)
        {
            var diskProvider = DispatchProxy.Create<IDiskProvider, TestDiskProvider>();
            ((TestDiskProvider)(object)diskProvider).PackageInfoPath = packageInfoPath;
            return diskProvider;
        }

        private sealed class TestAppFolderInfo : IAppFolderInfo
        {
            public TestAppFolderInfo(string startupFolder)
            {
                StartUpFolder = startupFolder;
            }

            public string AppDataFolder => "/data";
            public string TempFolder => "/tmp";
            public string StartUpFolder { get; }
        }

        private class TestDiskProvider : DispatchProxy
        {
            public string PackageInfoPath { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                return targetMethod.Name switch
                {
                    "FileExists" => string.Equals(args[0] as string, PackageInfoPath, StringComparison.Ordinal),
                    "ReadAllText" => "UpdateMethod=External\nUpdateMethodMessage=Use your package manager to update ChaptarrNG.\nBranch=main\n",
                    _ => throw new NotSupportedException($"Unexpected disk-provider call: {targetMethod.Name}")
                };
            }
        }
    }
}
