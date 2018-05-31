// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using IISIntegration.FunctionalTests.Utilities;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.CommandLineUtils;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Server.IISIntegration.FunctionalTests
{
    [SkipIfIISExpressSchemaMissingInProcess]
    public class StartupTests : IISFunctionalTestBase
    {
        public StartupTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ExpandEnvironmentVariableInWebConfig()
        {
            var dotnetLocation = DotNetMuxer.MuxerPathOrDefault();
            var deploymentParameters = GetBaseDeploymentParameters();

            // Point to dotnet installed in user profile.
            deploymentParameters.EnvironmentVariables["DotnetPath"] = dotnetLocation;

            var deploymentResult = await DeployAsync(deploymentParameters);

            ModifyAspNetCoreSectionInWebConfig(deploymentResult, "processPath", "%DotnetPath%");

            var response = await deploymentResult.RetryingHttpClient.GetAsync("HelloWorld");

            var responseText = await response.Content.ReadAsStringAsync();
            Assert.Equal("Hello World", responseText);
        }

        [Fact]
        public async Task InvalidProcessPath_ExpectServerError()
        {
            var dotnetLocation = "bogus";

            var deploymentParameters = GetBaseDeploymentParameters();
            // Point to dotnet installed in user profile.
            deploymentParameters.EnvironmentVariables["DotnetPath"] = Environment.ExpandEnvironmentVariables(dotnetLocation); // Path to dotnet.

            var deploymentResult = await DeployAsync(deploymentParameters);

            ModifyAspNetCoreSectionInWebConfig(deploymentResult, "processPath", "%DotnetPath%");

            // Request to base address and check if various parts of the body are rendered & measure the cold startup time.
            var response = await deploymentResult.RetryingHttpClient.GetAsync("HelloWorld");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }


        public static TestMatrix TestVariants
            => TestMatrix.ForServers(ServerType.IISExpress)
                .WithTfms(Tfm.NetCoreApp21)
                .WithAllApplicationTypes()
                .WithAncmV2InProcess();

        [ConditionalTheory]
        [MemberData(nameof(TestVariants))]
        public async Task HelloWorld(TestVariant variant)
        {
            var deploymentParameters = new DeploymentParameters(variant)
            {
                ApplicationPath = Helpers.GetInProcessTestSitesPath(),
            };

            var deploymentResult = await DeployAsync(deploymentParameters);

            var response = await deploymentResult.RetryingHttpClient.GetAsync("/HelloWorld");
            var responseText = await response.Content.ReadAsStringAsync();

            Assert.Equal("Hello World", responseText);
        }

        [Fact]
        public async Task DetectsOveriddenServer()
        {
            var deploymentResult = await DeployAsync(GetBaseDeploymentParameters("OverriddenServerWebSite"));
            var response = await deploymentResult.HttpClient.GetAsync("/");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Contains(TestSink.Writes, context => context.Message.Contains("Application is running inside IIS process but is not configured to use IIS server"));
        }

        private DeploymentParameters GetBaseDeploymentParameters(string site = null)
        {
            return new DeploymentParameters(Helpers.GetTestWebSitePath(site ?? "InProcessWebSite"), ServerType.IISExpress, RuntimeFlavor.CoreClr, RuntimeArchitecture.x64)
            {
                TargetFramework = Tfm.NetCoreApp21,
                ApplicationType = ApplicationType.Portable,
                AncmVersion = AncmVersion.AspNetCoreModuleV2
            };
        }

        private static void ModifyAspNetCoreSectionInWebConfig(IISDeploymentResult deploymentResult, string key, string value)
        {
            // modify the web.config after publish
            var root = deploymentResult.DeploymentResult.ContentRoot;
            var webConfigFile = $"{root}/web.config";
            var config = XDocument.Load(webConfigFile);
            var element = config.Descendants("aspNetCore").FirstOrDefault();
            element.SetAttributeValue(key, value);
            config.Save(webConfigFile);
        }
    }
}
