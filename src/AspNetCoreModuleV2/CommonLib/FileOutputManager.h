// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#pragma once

#include "sttimer.h"
#include "IOutputManager.h"
#include "HandleWrapper.h"
#include "StdWrapper.h"
#include "stringa.h"
#include "stringu.h"

class FileOutputManager : public IOutputManager
{
    #define FILE_FLUSH_TIMEOUT 3000
    #define MAX_FILE_READ_SIZE 30000
public:
    FileOutputManager(PCWSTR pwzApplicationPath, PCWSTR pwzStdOutLogFileName);
    FileOutputManager(PCWSTR pwzApplicationPath, PCWSTR pwzStdOutLogFileName, bool fEnableNativeLogging);
    ~FileOutputManager();

    virtual std::wstring GetStdOutContent() override;
    virtual HRESULT Start() override;
    virtual HRESULT Stop() override;

private:
    HandleWrapper<InvalidHandleTraits> m_hLogFileHandle;
    std::wstring m_wsStdOutLogFileName;
    std::filesystem::path m_wsApplicationPath;
    std::filesystem::path m_struLogFilePath;
    std::string m_straFileContent;
    bool m_disposed;
    bool m_fEnableNativeRedirection;
    SRWLOCK m_srwLock{};
    std::unique_ptr<StdWrapper>    stdoutWrapper;
    std::unique_ptr<StdWrapper>    stderrWrapper;
    CHAR            pzFileContents[MAX_FILE_READ_SIZE] = { 0 };
    DWORD           dwNumBytesRead;

};
