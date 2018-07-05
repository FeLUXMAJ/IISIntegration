// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.IISIntegration.FunctionalTests;
using Microsoft.Extensions.Logging.Testing;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Server.IntegrationTesting
{
    public class FunctionalTestsBase : LoggedTest
    {
        private const string DebugEnvironmentVariable = "ASPNETCORE_MODULE_DEBUG";
        private const string LogFile = "";

        public FunctionalTestsBase(ITestOutputHelper output = null) : base(output)
        {
        }

        private ApplicationDeployer _deployer;

        protected virtual async Task<IISDeploymentResult> DeployAsync(DeploymentParameters parameters)
        {
            if (!parameters.EnvironmentVariables.ContainsKey(DebugEnvironmentVariable))
            {
                parameters.EnvironmentVariables[DebugEnvironmentVariable] = "4";
            }

            if (parameters.ServerType == ServerType.IIS)
            {
                // Currently hosting throws if the Servertype = IIS.
                parameters.EnvironmentVariables[DebugEnvironmentVariable] = "";
                _deployer = new IISDeployer(parameters, LoggerFactory);
            }
            else if (parameters.ServerType == ServerType.IISExpress)
            {
                _deployer = new IISExpressDeployer(parameters, LoggerFactory);
            }

            var result = await _deployer.DeployAsync();

            return new IISDeploymentResult(result, Logger);
        }

        public override void Dispose()
        {
            StopServer();
        }

        public void StopServer()
        {
            _deployer?.Dispose();
            _deployer = null;
        }
    }
}
