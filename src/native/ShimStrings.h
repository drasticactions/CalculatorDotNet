#pragma once

#include <cstdint>
#include <cstring>
#include <string>

namespace shim
{
    inline std::u16string ToUtf16(const std::wstring& ws)
    {
        if constexpr (sizeof(wchar_t) == sizeof(char16_t))
        {
            return std::u16string(reinterpret_cast<const char16_t*>(ws.data()), ws.size());
        }
        else
        {
            std::u16string out;
            out.reserve(ws.size());
            for (wchar_t wc : ws)
            {
                uint32_t cp = static_cast<uint32_t>(wc);
                if (cp <= 0xFFFF)
                {
                    out.push_back(static_cast<char16_t>(cp));
                }
                else
                {
                    cp -= 0x10000;
                    out.push_back(static_cast<char16_t>(0xD800 + (cp >> 10)));
                    out.push_back(static_cast<char16_t>(0xDC00 + (cp & 0x3FF)));
                }
            }
            return out;
        }
    }

    inline std::wstring FromUtf16(const char16_t* s)
    {
        std::wstring out;
        if (s == nullptr)
        {
            return out;
        }
        if constexpr (sizeof(wchar_t) == sizeof(char16_t))
        {
            return std::wstring(reinterpret_cast<const wchar_t*>(s));
        }
        else
        {
            while (*s != 0)
            {
                char16_t c = *s++;
                if (c >= 0xD800 && c <= 0xDBFF && *s >= 0xDC00 && *s <= 0xDFFF)
                {
                    uint32_t cp = 0x10000 + ((static_cast<uint32_t>(c) - 0xD800) << 10) + (static_cast<uint32_t>(*s) - 0xDC00);
                    ++s;
                    out.push_back(static_cast<wchar_t>(cp));
                }
                else
                {
                    out.push_back(static_cast<wchar_t>(c));
                }
            }
            return out;
        }
    }

    inline char16_t* AllocUtf16(const std::wstring& ws)
    {
        std::u16string u16 = ToUtf16(ws);
        char16_t* buffer = new char16_t[u16.size() + 1];
        std::memcpy(buffer, u16.data(), u16.size() * sizeof(char16_t));
        buffer[u16.size()] = 0;
        return buffer;
    }
}
