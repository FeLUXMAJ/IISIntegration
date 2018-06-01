// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "gtest/internal/gtest-port.h"

class PipeManagerWrapper
{
public:
    FileOutputManager* manager;
    PipeManagerWrapper(FileOutputManager* m)
        : manager(m)
    {
        manager->Start();
    }

    ~PipeManagerWrapper()
    {
        delete manager;
    }
};

namespace FileOutManagerStartupTests
{
    using ::testing::Test;
    class FileOutputManagerTest : public Test
    {
    protected:
        void
            Test(std::wstring fileNamePrefix)
        {
            PCWSTR expected = L"test";

            std::wstring tempDirectory = Helpers::CreateRandomTempDirectory();
            FileOutputManager* pManager = new FileOutputManager(fileNamePrefix.c_str(), tempDirectory.c_str());
            {
                PipeManagerWrapper wrapper(pManager);

                wprintf(expected);
            }
          
            // std::filesystem is available on c++17, however gtest fails to build when using it
            // c++14 has filesystem as experimental.
            for (auto & p : std::experimental::filesystem::directory_iterator(tempDirectory))
            {
                std::wstring filename(p.path().filename());
                ASSERT_EQ(filename.substr(0, fileNamePrefix.size()), fileNamePrefix);

                std::wstring content = Helpers::ReadFileContent(std::wstring(p.path()));
                ASSERT_EQ(content.length(), DWORD(4));
                ASSERT_STREQ(content.c_str(), expected);
            }

            Helpers::DeleteDirectory(tempDirectory);
        }
    };

    TEST_F(FileOutputManagerTest, WriteToFileCheckContentsWritten)
    {
        Test(L"");
        Test(L"log");
    }
}

namespace FileOutManagerOutputTests
{

    TEST(FileOutManagerOutputTest, StdErr)
    {
        PCWSTR expected = L"test";

        std::wstring tempDirectory = Helpers::CreateRandomTempDirectory();

        FileOutputManager* pManager = new FileOutputManager(L"", tempDirectory.c_str());
        {
            PipeManagerWrapper wrapper(pManager);

            wprintf(expected, stderr);
            STRU struContent;
            ASSERT_TRUE(pManager->GetStdOutContent(&struContent));

            ASSERT_STREQ(struContent.QueryStr(), expected);
        }

        Helpers::DeleteDirectory(tempDirectory);
    }

    TEST(FileOutManagerOutputTest, CheckFileOutput)
    {
        PCWSTR expected = L"test";

        std::wstring tempDirectory = Helpers::CreateRandomTempDirectory();

        FileOutputManager* pManager = new FileOutputManager(L"", tempDirectory.c_str());
        {
            PipeManagerWrapper wrapper(pManager);

            wprintf(expected);
            STRU struContent;
            ASSERT_TRUE(pManager->GetStdOutContent(&struContent));

            ASSERT_STREQ(struContent.QueryStr(), expected);
        }

        Helpers::DeleteDirectory(tempDirectory);
    }

    TEST(FileOutManagerOutputTest, CapAt4KB)
    {
        PCWSTR expected = L"test";

        std::wstring tempDirectory = Helpers::CreateRandomTempDirectory();

        FileOutputManager* pManager = new FileOutputManager(L"", tempDirectory.c_str());
        {
            PipeManagerWrapper wrapper(pManager);

            for (int i = 0; i < 1200; i++)
            {
                wprintf(expected);
            }

            STRU struContent;
            ASSERT_TRUE(pManager->GetStdOutContent(&struContent));

            ASSERT_EQ(struContent.QueryCCH(), 4096);
        }

        Helpers::DeleteDirectory(tempDirectory);
    }
}

