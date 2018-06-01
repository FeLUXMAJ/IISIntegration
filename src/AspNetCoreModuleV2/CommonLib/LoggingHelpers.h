// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#pragma once

class LoggingHelpers
{
public:

    static
    HRESULT
    CreateLoggingProvider(
        bool fLoggingEnabled,
        bool fIsConsoleWindows,
        PCWSTR pwzStdOutFileName,
        PCWSTR pwzApplicationPath,
        _Out_ IOutputManager** outputManager
    );
};

