#include <windows.h>
#include <shlobj.h>
#include <shellapi.h>

#include <string>

namespace
{
    constexpr wchar_t ScreenSaverRelativePath[] = L"Aerial\\Aerial.scr";

    std::wstring GetScreenSaverPath()
    {
        PWSTR programFiles = nullptr;
        if (FAILED(SHGetKnownFolderPath(FOLDERID_ProgramFilesX64, 0, nullptr, &programFiles)))
            return {};

        std::wstring path = programFiles;
        CoTaskMemFree(programFiles);
        path += L"\\";
        path += ScreenSaverRelativePath;
        return path;
    }

    bool SetRegistryValues(const std::wstring& screenSaverPath)
    {
        HKEY key;
        if (RegCreateKeyExW(
                HKEY_CURRENT_USER,
                L"Control Panel\\Desktop",
                0,
                nullptr,
                0,
                KEY_SET_VALUE,
                nullptr,
                &key,
                nullptr) != ERROR_SUCCESS)
            return false;

        const auto setString = [&](const wchar_t* name, const std::wstring& value)
        {
            return RegSetValueExW(
                       key,
                       name,
                       0,
                       REG_SZ,
                       reinterpret_cast<const BYTE*>(value.c_str()),
                       static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t))) == ERROR_SUCCESS;
        };

        const DWORD enabled = 1;
        const bool success =
            setString(L"SCRNSAVE.EXE", screenSaverPath) &&
            RegSetValueExW(
                key,
                L"ScreenSaveActive",
                0,
                REG_DWORD,
                reinterpret_cast<const BYTE*>(&enabled),
                sizeof(enabled)) == ERROR_SUCCESS &&
            setString(L"ScreenSaveTimeOut", L"300");

        RegCloseKey(key);
        return success;
    }

    void UpdatePerUserSystemParameters()
    {
        SHELLEXECUTEINFOW executeInfo{ sizeof(executeInfo) };
        executeInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
        executeInfo.lpFile = L"rundll32.exe";
        executeInfo.lpParameters = L"user32.dll,UpdatePerUserSystemParameters";
        executeInfo.nShow = SW_HIDE;

        if (ShellExecuteExW(&executeInfo))
        {
            WaitForSingleObject(executeInfo.hProcess, INFINITE);
            CloseHandle(executeInfo.hProcess);
        }
    }

    void OpenScreenSaverSettings()
    {
        ShellExecuteW(
            nullptr,
            L"open",
            L"rundll32.exe",
            L"shell32.dll,Control_RunDLL desk.cpl,,1",
            nullptr,
            SW_SHOWNORMAL);
    }
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    const std::wstring screenSaverPath = GetScreenSaverPath();
    const DWORD attributes = GetFileAttributesW(screenSaverPath.c_str());
    if (screenSaverPath.empty() ||
        attributes == INVALID_FILE_ATTRIBUTES ||
        (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
    {
        const std::wstring message = L"Screensaver not found: " + screenSaverPath;
        MessageBoxW(nullptr, message.c_str(), L"Aerial", MB_OK | MB_ICONEXCLAMATION);
        return 1;
    }

    if (!SetRegistryValues(screenSaverPath))
    {
        MessageBoxW(nullptr, L"Unable to configure the Aerial screen saver.", L"Aerial", MB_OK | MB_ICONEXCLAMATION);
        return 1;
    }

    UpdatePerUserSystemParameters();
    OpenScreenSaverSettings();
    return 0;
}