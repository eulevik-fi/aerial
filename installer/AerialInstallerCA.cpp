#include <windows.h>
#include <msi.h>
#include <msiquery.h>
#include <objbase.h>

#include <cwchar>
#include <iterator>
#include <string>
#include <vector>

namespace
{
    constexpr wchar_t UninstallKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
    constexpr wchar_t ProductDisplayName[] = L"Aerial Screen Saver";
    constexpr wchar_t ProductPublisher[] = L"Eric Ulevik";

    class RegistryKey
    {
    public:
        explicit RegistryKey(HKEY key = nullptr) : key_(key) {}
        ~RegistryKey()
        {
            if (key_)
                RegCloseKey(key_);
        }

        RegistryKey(const RegistryKey&) = delete;
        RegistryKey& operator=(const RegistryKey&) = delete;

        HKEY* put() { return &key_; }
        HKEY get() const { return key_; }

    private:
        HKEY key_;
    };

    bool ReadStringValue(HKEY key, const wchar_t* name, std::wstring& value)
    {
        DWORD type = 0;
        DWORD size = 0;
        LONG result = RegGetValueW(
            key, nullptr, name, RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ,
            &type, nullptr, &size);
        if (result != ERROR_SUCCESS || size < sizeof(wchar_t))
            return false;

        std::vector<wchar_t> buffer(size / sizeof(wchar_t) + 1, L'\0');
        result = RegGetValueW(
            key, nullptr, name, RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ,
            &type, buffer.data(), &size);
        if (result != ERROR_SUCCESS)
            return false;

        value.assign(buffer.data());
        return true;
    }

    bool GetProductCode(const wchar_t* subkey, std::wstring& productCode)
    {
        GUID guid{};
        if (FAILED(CLSIDFromString(const_cast<LPOLESTR>(subkey), &guid)))
            return false;

        wchar_t formatted[40]{};
        if (StringFromGUID2(guid, formatted, static_cast<int>(std::size(formatted))) == 0)
            return false;

        productCode.assign(formatted);
        return true;
    }

    bool ContainsProduct(const std::wstring& productCode, const std::vector<std::wstring>& products)
    {
        for (const auto& product : products)
        {
            if (_wcsicmp(product.c_str(), productCode.c_str()) == 0)
                return true;
        }
        return false;
    }

    bool ContainsProduct(const std::wstring& productCode, const std::wstring& property)
    {
        size_t start = 0;
        while (start < property.size())
        {
            const size_t end = property.find(L';', start);
            const size_t length = (end == std::wstring::npos ? property.size() : end) - start;
            if (length == productCode.size() &&
                _wcsnicmp(property.data() + start, productCode.c_str(), length) == 0)
                return true;
            if (end == std::wstring::npos)
                break;
            start = end + 1;
        }
        return false;
    }

    UINT EnumerateUninstallEntries(REGSAM registryView, std::vector<std::wstring>& products)
    {
        RegistryKey uninstall;
        LONG result = RegOpenKeyExW(
            HKEY_LOCAL_MACHINE, UninstallKey, 0,
            KEY_ENUMERATE_SUB_KEYS | registryView, uninstall.put());
        if (result == ERROR_FILE_NOT_FOUND)
            return ERROR_SUCCESS;
        if (result != ERROR_SUCCESS)
            return ERROR_INSTALL_FAILURE;

        for (DWORD index = 0;; ++index)
        {
            wchar_t subkey[256]{};
            DWORD length = static_cast<DWORD>(std::size(subkey));
            result = RegEnumKeyExW(uninstall.get(), index, subkey, &length, nullptr, nullptr, nullptr, nullptr);
            if (result == ERROR_NO_MORE_ITEMS)
                break;
            if (result != ERROR_SUCCESS)
                return ERROR_INSTALL_FAILURE;

            std::wstring productCode;
            if (!GetProductCode(subkey, productCode))
                continue;

            RegistryKey entry;
            result = RegOpenKeyExW(uninstall.get(), subkey, 0, KEY_QUERY_VALUE, entry.put());
            if (result == ERROR_FILE_NOT_FOUND)
                continue;
            if (result != ERROR_SUCCESS)
                return ERROR_INSTALL_FAILURE;

            std::wstring displayName;
            std::wstring publisher;
            if (ReadStringValue(entry.get(), L"DisplayName", displayName) &&
                ReadStringValue(entry.get(), L"Publisher", publisher) &&
                _wcsicmp(displayName.c_str(), ProductDisplayName) == 0 &&
                _wcsicmp(publisher.c_str(), ProductPublisher) == 0 &&
                !ContainsProduct(productCode, products))
            {
                products.push_back(std::move(productCode));
            }
        }

        return ERROR_SUCCESS;
    }

    UINT GetMsiProperty(MSIHANDLE install, const wchar_t* name, std::wstring& value)
    {
        DWORD length = 0;
        wchar_t emptyBuffer[1]{};
        UINT result = MsiGetProperty(install, name, emptyBuffer, &length);
        if (result != ERROR_MORE_DATA && result != ERROR_SUCCESS)
            return result;
        if (length == 0)
        {
            value.clear();
            return ERROR_SUCCESS;
        }

        std::vector<wchar_t> buffer(static_cast<size_t>(length) + 1);
        DWORD bufferLength = length + 1;
        result = MsiGetProperty(install, name, buffer.data(), &bufferLength);
        if (result != ERROR_SUCCESS)
            return result;

        value.assign(buffer.data());
        return ERROR_SUCCESS;
    }
}

extern "C" __declspec(dllexport) UINT __stdcall FindPreviousPerMachineInstallations(MSIHANDLE install)
{
    std::vector<std::wstring> products;
    UINT result = EnumerateUninstallEntries(KEY_WOW64_64KEY, products);
    if (result != ERROR_SUCCESS)
        return result;

    result = EnumerateUninstallEntries(KEY_WOW64_32KEY, products);
    if (result != ERROR_SUCCESS)
        return result;

    if (products.empty())
        return ERROR_SUCCESS;

    std::wstring detectedProducts;
    result = GetMsiProperty(install, L"WIX_UPGRADE_DETECTED", detectedProducts);
    if (result != ERROR_SUCCESS)
        return result;

    for (const auto& product : products)
    {
        if (ContainsProduct(product, detectedProducts))
            continue;
        if (!detectedProducts.empty())
            detectedProducts += L";";
        detectedProducts += product;
    }

    return MsiSetProperty(install, L"WIX_UPGRADE_DETECTED", detectedProducts.c_str());
}
