// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests.Utilities;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Server.IntegrationTesting.IIS;
using Microsoft.AspNetCore.Testing.xunit;
using Xunit;

namespace Microsoft.AspNetCore.Server.IISIntegration.FunctionalTests
{
    [Collection(PublishedSitesCollection.Name)]
    public class StartupTests : IISFunctionalTestBase
    {
        private readonly PublishedSitesFixture _fixture;

        public StartupTests(PublishedSitesFixture fixture)
        {
            _fixture = fixture;
        }

        private readonly string _dotnetLocation = DotNetCommands.GetDotNetExecutable(RuntimeArchitecture.x64);

        [ConditionalFact]
        public async Task ExpandEnvironmentVariableInWebConfig()
        {
            // Point to dotnet installed in user profile.
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);
            deploymentParameters.EnvironmentVariables["DotnetPath"] = _dotnetLocation;
            deploymentParameters.WebConfigActionList.Add(WebConfigHelpers.AddOrModifyAspNetCoreSection("processPath", "%DotnetPath%"));
            await StartAsync(deploymentParameters);
        }

        [ConditionalTheory]
        [InlineData("bogus", "", @"Executable was not found at '.*?\\bogus.exe")]
        [InlineData("c:\\random files\\dotnet.exe", "something.dll", @"Could not find dotnet.exe at '.*?\\dotnet.exe'")]
        [InlineData(".\\dotnet.exe", "something.dll", @"Could not find dotnet.exe at '.*?\\.\\dotnet.exe'")]
        [InlineData("dotnet.exe", "", @"Application arguments are empty.")]
        [InlineData("dotnet.zip", "", @"Process path 'dotnet.zip' doesn't have '.exe' extension.")]
        public async Task InvalidProcessPath_ExpectServerError(string path, string arguments, string subError)
        {
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);
            deploymentParameters.WebConfigActionList.Add(WebConfigHelpers.AddOrModifyAspNetCoreSection("processPath", path));
            deploymentParameters.WebConfigActionList.Add(WebConfigHelpers.AddOrModifyAspNetCoreSection("arguments", arguments));

            var deploymentResult = await DeployAsync(deploymentParameters);

            var response = await deploymentResult.HttpClient.GetAsync("HelloWorld");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            StopServer();

            EventLogHelpers.VerifyEventLogEvent(deploymentResult, TestSink, $@"Application '{Regex.Escape(deploymentResult.ContentRoot)}\\' wasn't able to start. {subError}");
        }

        [ConditionalFact]
        public async Task StartsWithDotnetLocationWithoutExe()
        {
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);

            var dotnetLocationWithoutExtension = _dotnetLocation.Substring(0, _dotnetLocation.LastIndexOf("."));
            deploymentParameters.WebConfigActionList.Add(WebConfigHelpers.AddOrModifyAspNetCoreSection("processPath", dotnetLocationWithoutExtension));

            await StartAsync(deploymentParameters);
        }

        [ConditionalFact]
        public async Task StartsWithDotnetLocationUppercase()
        {
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);

            var dotnetLocationWithoutExtension = _dotnetLocation.Substring(0, _dotnetLocation.LastIndexOf(".")).ToUpperInvariant();
            deploymentParameters.WebConfigActionList.Add(WebConfigHelpers.AddOrModifyAspNetCoreSection("processPath", dotnetLocationWithoutExtension));

            await StartAsync(deploymentParameters);
        }

        [ConditionalTheory]
        [InlineData("dotnet")]
        [InlineData("dotnet.EXE")]
        public async Task StartsWithDotnetOnThePath(string path)
        {
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);

            deploymentParameters.EnvironmentVariables["PATH"] = Path.GetDirectoryName(_dotnetLocation);
            deploymentParameters.WebConfigActionList.Add(WebConfigHelpers.AddOrModifyAspNetCoreSection("processPath", path));

            var deploymentResult = await DeployAsync(deploymentParameters);
            await deploymentResult.AssertStarts();

            // Verify that in this scenario where.exe was invoked only once by shim and request handler uses cached value
            Assert.Equal(1, TestSink.Writes.Count(w => w.Message.Contains("Invoking where.exe to find dotnet.exe")));
        }

        public static TestMatrix TestVariants
            => TestMatrix.ForServers(DeployerSelector.ServerType)
                .WithTfms(Tfm.NetCoreApp22)
                .WithAllApplicationTypes()
                .WithAncmV2InProcess();

        [ConditionalTheory]
        [MemberData(nameof(TestVariants))]
        public async Task HelloWorld(TestVariant variant)
        {
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(variant, publish: true);
            await StartAsync(deploymentParameters);
        }

        [ConditionalFact]
        public async Task StartsWithPortableAndBootstraperExe()
        {
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(_fixture.InProcessTestSite, publish: true);
            // rest publisher as it doesn't support additional parameters
            deploymentParameters.ApplicationPublisher = null;
            // ReferenceTestTasks is workaround for https://github.com/dotnet/sdk/issues/2482
            deploymentParameters.AdditionalPublishParameters = "-p:RuntimeIdentifier=win7-x64 -p:UseAppHost=true -p:SelfContained=false -p:ReferenceTestTasks=false";
            deploymentParameters.RestoreOnPublish = true;
            var deploymentResult = await DeployAsync(deploymentParameters);

            Assert.True(File.Exists(Path.Combine(deploymentResult.ContentRoot, "InProcessWebSite.exe")));
            Assert.False(File.Exists(Path.Combine(deploymentResult.ContentRoot, "hostfxr.dll")));
            Assert.Contains("InProcessWebSite.exe", File.ReadAllText(Path.Combine(deploymentResult.ContentRoot, "web.config")));

            await deploymentResult.AssertStarts();
        }

        [ConditionalFact]
        public async Task DetectsOveriddenServer()
        {
            var deploymentResult = await DeployAsync(_fixture.GetBaseDeploymentParameters(_fixture.OverriddenServerWebSite, publish: true));
            var response = await deploymentResult.HttpClient.GetAsync("/");
            Assert.False(response.IsSuccessStatusCode);

            StopServer();

            Assert.Contains(TestSink.Writes, context => context.Message.Contains("Application is running inside IIS process but is not configured to use IIS server"));
        }

        [ConditionalFact]
        public async Task CheckInvalidHostingModelParameter()
        {
            var deploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);
            deploymentParameters.WebConfigActionList.Add(WebConfigHelpers.AddOrModifyAspNetCoreSection("hostingModel", "bogus"));

            var deploymentResult = await DeployAsync(deploymentParameters);

            var response = await deploymentResult.HttpClient.GetAsync("HelloWorld");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            StopServer();

            EventLogHelpers.VerifyEventLogEvent(deploymentResult, TestSink, "Unknown hosting model 'bogus'. Please specify either hostingModel=\"inprocess\" or hostingModel=\"outofprocess\" in the web.config file.");
        }


        private static Dictionary<string, (string, Action<XElement>)> InvalidConfigTransformations = InitInvalidConfigTransformations();
        public static IEnumerable<object[]> InvalidConfigTransformationsScenarios => InvalidConfigTransformations.ToTheoryData();

        [ConditionalTheory]
        [MemberData(nameof(InvalidConfigTransformationsScenarios))]
        public async Task ReportsWebConfigAuthoringErrors(string scenario)
        {
            var (expectedError, action) = InvalidConfigTransformations[scenario];
            var iisDeploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);
            iisDeploymentParameters.WebConfigActionList.Add((element, _) => action(element));
            var deploymentResult = await DeployAsync(iisDeploymentParameters);
            var result = await deploymentResult.HttpClient.GetAsync("/HelloWorld");
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);

            StopServer();
            EventLogHelpers.VerifyEventLogEvent(deploymentResult, TestSink, "Configuration load error. " + expectedError);
        }

        public static Dictionary<string, (string, Action<XElement>)> InitInvalidConfigTransformations()
        {
            var dictionary = new Dictionary<string, (string, Action<XElement>)>();
            dictionary.Add("Empty process path",
                (
                    "Attribute 'processPath' is required.",
                    element => element.Descendants("aspNetCore").Single().SetAttributeValue("processPath", "")
                ));
            dictionary.Add("Unknown hostingModel",
                (
                    "Unknown hosting model 'asdf'.",
                    element => element.Descendants("aspNetCore").Single().SetAttributeValue("hostingModel", "asdf")
                ));
            dictionary.Add("environmentVariables with add",
                (
                    "Unable to get required configuration section 'system.webServer/aspNetCore'. Possible reason is web.config authoring error.",
                    element => element.Descendants("aspNetCore").Single().GetOrAdd("environmentVariables").GetOrAdd("add")
                ));
            return dictionary;
        }

        private static Dictionary<string, Func<IISDeploymentParameters, string>> PortableConfigTransformations = InitPortableWebConfigTransformations();
        public static IEnumerable<object[]> PortableConfigTransformationsScenarios => PortableConfigTransformations.ToTheoryData();

        [ConditionalTheory]
        [MemberData(nameof(PortableConfigTransformationsScenarios))]
        public async Task StartsWithWebConfigVariationsPortable(string scenario)
        {
            var action = PortableConfigTransformations[scenario];
            var iisDeploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);
            var expectedArguments = action(iisDeploymentParameters);
            var result = await DeployAsync(iisDeploymentParameters);
            Assert.Equal(expectedArguments, await result.HttpClient.GetStringAsync("/CommandLineArgs"));
        }

        public static Dictionary<string, Func<IISDeploymentParameters, string>> InitPortableWebConfigTransformations()
        {
            var dictionary = new Dictionary<string, Func<IISDeploymentParameters, string>>();
            var pathWithSpace = "\u03c0 \u2260 3\u00b714";

            dictionary.Add("App in bin subdirectory full path to dll using exec and quotes",
                parameters => {
                    MoveApplication(parameters, "bin");
                    TransformArguments(parameters, (arguments, root) => "exec " + Path.Combine(root, "bin", arguments));
                    return "";
                });

            dictionary.Add("App in subdirectory with space",
                parameters => {
                    MoveApplication(parameters, pathWithSpace);
                    TransformArguments(parameters, (arguments, root) => Path.Combine(pathWithSpace, arguments));
                    return "";
                });

            dictionary.Add("App in subdirectory with space and full path to dll",
                parameters => {
                    MoveApplication(parameters, pathWithSpace);
                    TransformArguments(parameters, (arguments, root) => Path.Combine(root, pathWithSpace, arguments));
                    return "";
                });

            dictionary.Add("App in bin subdirectory with space full path to dll using exec and quotes",
                parameters => {
                    MoveApplication(parameters, pathWithSpace);
                    TransformArguments(parameters, (arguments, root) => "exec \"" + Path.Combine(root, pathWithSpace, arguments) + "\" extra arguments");
                    return "extra|arguments";
                });

            dictionary.Add("App in bin subdirectory and quoted argument",
                parameters => {
                    MoveApplication(parameters, "bin");
                    TransformArguments(parameters, (arguments, root) => Path.Combine("bin", arguments) + " \"extra argument\"");
                    return "extra argument";
                });

            dictionary.Add("App in bin subdirectory full path to dll",
                parameters => {
                    MoveApplication(parameters, "bin");
                    TransformArguments(parameters, (arguments, root) => Path.Combine(root, "bin", arguments) + " extra arguments");
                    return "extra|arguments";
                });
            return dictionary;
        }


        private static Dictionary<string, Func<IISDeploymentParameters, string>> StandaloneConfigTransformations = InitStandaloneConfigTransformations();
        public static IEnumerable<object[]> StandaloneConfigTransformationsScenarios => StandaloneConfigTransformations.ToTheoryData();

        [ConditionalTheory]
        [MemberData(nameof(StandaloneConfigTransformationsScenarios))]
        public async Task StartsWithWebConfigVariationsStandalone(string scenario)
        {
            var action = StandaloneConfigTransformations[scenario];
            var iisDeploymentParameters = _fixture.GetBaseDeploymentParameters(publish: true);
            iisDeploymentParameters.ApplicationType = ApplicationType.Standalone;
            var expectedArguments = action(iisDeploymentParameters);
            var result = await DeployAsync(iisDeploymentParameters);
            Assert.Equal(expectedArguments, await result.HttpClient.GetStringAsync("/CommandLineArgs"));
        }

        public static Dictionary<string, Func<IISDeploymentParameters, string>> InitStandaloneConfigTransformations()
        {
            var dictionary = new Dictionary<string, Func<IISDeploymentParameters, string>>();
            var pathWithSpace = "\u03c0 \u2260 3\u00b714";

            dictionary.Add("App in subdirectory",
                parameters => {
                    MoveApplication(parameters, pathWithSpace);
                    TransformPath(parameters, (path, root) => Path.Combine(pathWithSpace, path));
                    TransformArguments(parameters, (arguments, root) => "\"additional argument\"");
                    return "additional argument";
                });

            dictionary.Add("App in bin subdirectory full path",
                parameters => {
                    MoveApplication(parameters, pathWithSpace);
                    TransformPath(parameters, (path, root) => Path.Combine(root, pathWithSpace, path));
                    TransformArguments(parameters, (arguments, root) => "additional arguments");
                    return "additional|arguments";
                });

            return dictionary;
        }

        private static void MoveApplication(
            IISDeploymentParameters parameters,
            string subdirectory)
        {
            parameters.WebConfigActionList.Add((config, contentRoot) =>
            {
                var source = new DirectoryInfo(contentRoot);
                var subDirectoryPath = source.CreateSubdirectory(subdirectory);

                // Copy everything into a subfolder
                Helpers.CopyFiles(source, subDirectoryPath, null);
                // Cleanup files
                foreach (var fileSystemInfo in source.GetFiles())
                {
                    fileSystemInfo.Delete();
                }
            });
        }

        private static void TransformPath(IISDeploymentParameters parameters, Func<string, string, string> transformation)
        {
            parameters.WebConfigActionList.Add(
                (config, contentRoot) =>
                {
                    var aspNetCoreElement = config.Descendants("aspNetCore").Single();
                    aspNetCoreElement.SetAttributeValue("processPath", transformation((string)aspNetCoreElement.Attribute("processPath"), contentRoot));
                });
        }

        private static void TransformArguments(IISDeploymentParameters parameters, Func<string, string, string> transformation)
        {
            parameters.WebConfigActionList.Add(
                (config, contentRoot) =>
                {
                    var aspNetCoreElement = config.Descendants("aspNetCore").Single();
                    aspNetCoreElement.SetAttributeValue("arguments", transformation((string)aspNetCoreElement.Attribute("arguments"), contentRoot));
                });
        }

    }
}
