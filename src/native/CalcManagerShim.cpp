#include <cstdint>
#include <cstring>
#include <memory>
#include <string>
#include <vector>

#include "ShimStrings.h"
#include "CalculatorManager.h"
#include "CalculatorResource.h"
#include "Command.h"

#if defined(_WIN32)
#define CALCSHIM_EXPORT extern "C" __declspec(dllexport)
#else
#define CALCSHIM_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace
{
    using shim::ToUtf16;
    using shim::FromUtf16;
    using shim::AllocUtf16;
}

extern "C"
{
    typedef struct CalcShimCallbacks
    {
        void (*set_primary_display)(void* state, const char16_t* text, int32_t is_error);
        void (*set_is_in_error)(void* state, int32_t is_in_error);
        void (*set_expression_display)(void* state, const char16_t* const* token_strings, const int32_t* token_ids, int32_t count);
        void (*set_parenthesis_number)(void* state, uint32_t count);
        void (*on_no_right_paren_added)(void* state);
        void (*max_digits_reached)(void* state);
        void (*binary_operator_received)(void* state);
        void (*on_history_item_added)(void* state, uint32_t index);
        void (*set_memorized_numbers)(void* state, const char16_t* const* numbers, int32_t count);
        void (*memory_item_changed)(void* state, uint32_t index);
        void (*input_changed)(void* state);
        // Returned pointer must remain valid until the manager is destroyed; the shim
        // copies it before returning. Null is treated as the empty string.
        const char16_t* (*get_cengine_string)(void* state, const char16_t* id);
    } CalcShimCallbacks;
}

namespace
{
    class ShimHost final : public ICalcDisplay, public CalculationManager::IResourceProvider
    {
    public:
        ShimHost(const CalcShimCallbacks& callbacks, void* state) : m_cb(callbacks), m_state(state)
        {
        }

        // ICalcDisplay
        void SetPrimaryDisplay(const std::wstring& text, bool isError) override
        {
            if (m_cb.set_primary_display != nullptr)
            {
                std::u16string u16 = ToUtf16(text);
                m_cb.set_primary_display(m_state, u16.c_str(), isError ? 1 : 0);
            }
        }

        void SetIsInError(bool isInError) override
        {
            if (m_cb.set_is_in_error != nullptr)
            {
                m_cb.set_is_in_error(m_state, isInError ? 1 : 0);
            }
        }

        void SetExpressionDisplay(
            std::shared_ptr<std::vector<std::pair<std::wstring, int>>> const& tokens,
            std::shared_ptr<std::vector<std::shared_ptr<IExpressionCommand>>> const& /*commands*/) override
        {
            if (m_cb.set_expression_display == nullptr)
            {
                return;
            }
            std::vector<std::u16string> storage;
            std::vector<const char16_t*> strings;
            std::vector<int32_t> ids;
            if (tokens != nullptr)
            {
                storage.reserve(tokens->size());
                strings.reserve(tokens->size());
                ids.reserve(tokens->size());
                for (const auto& token : *tokens)
                {
                    storage.push_back(ToUtf16(token.first));
                    ids.push_back(token.second);
                }
                for (const auto& s : storage)
                {
                    strings.push_back(s.c_str());
                }
            }
            m_cb.set_expression_display(m_state, strings.data(), ids.data(), static_cast<int32_t>(strings.size()));
        }

        void SetParenthesisNumber(unsigned int count) override
        {
            if (m_cb.set_parenthesis_number != nullptr)
            {
                m_cb.set_parenthesis_number(m_state, count);
            }
        }

        void OnNoRightParenAdded() override
        {
            if (m_cb.on_no_right_paren_added != nullptr)
            {
                m_cb.on_no_right_paren_added(m_state);
            }
        }

        void MaxDigitsReached() override
        {
            if (m_cb.max_digits_reached != nullptr)
            {
                m_cb.max_digits_reached(m_state);
            }
        }

        void BinaryOperatorReceived() override
        {
            if (m_cb.binary_operator_received != nullptr)
            {
                m_cb.binary_operator_received(m_state);
            }
        }

        void OnHistoryItemAdded(unsigned int addedItemIndex) override
        {
            if (m_cb.on_history_item_added != nullptr)
            {
                m_cb.on_history_item_added(m_state, addedItemIndex);
            }
        }

        void SetMemorizedNumbers(const std::vector<std::wstring>& memorizedNumbers) override
        {
            if (m_cb.set_memorized_numbers == nullptr)
            {
                return;
            }
            std::vector<std::u16string> storage;
            std::vector<const char16_t*> strings;
            storage.reserve(memorizedNumbers.size());
            strings.reserve(memorizedNumbers.size());
            for (const auto& number : memorizedNumbers)
            {
                storage.push_back(ToUtf16(number));
            }
            for (const auto& s : storage)
            {
                strings.push_back(s.c_str());
            }
            m_cb.set_memorized_numbers(m_state, strings.data(), static_cast<int32_t>(strings.size()));
        }

        void MemoryItemChanged(unsigned int indexOfMemory) override
        {
            if (m_cb.memory_item_changed != nullptr)
            {
                m_cb.memory_item_changed(m_state, indexOfMemory);
            }
        }

        void InputChanged() override
        {
            if (m_cb.input_changed != nullptr)
            {
                m_cb.input_changed(m_state);
            }
        }

        // IResourceProvider
        std::wstring GetCEngineString(std::wstring_view id) override
        {
            if (m_cb.get_cengine_string == nullptr)
            {
                return {};
            }
            std::u16string u16Id = ToUtf16(std::wstring(id));
            const char16_t* value = m_cb.get_cengine_string(m_state, u16Id.c_str());
            return FromUtf16(value);
        }

    private:
        CalcShimCallbacks m_cb;
        void* m_state;
    };

    struct ManagerHandle
    {
        std::unique_ptr<ShimHost> host;
        std::unique_ptr<CalculationManager::CalculatorManager> manager;
    };

    ManagerHandle* AsHandle(void* handle)
    {
        return static_cast<ManagerHandle*>(handle);
    }
}

CALCSHIM_EXPORT void* CalcShim_Create(const CalcShimCallbacks* callbacks, void* state)
{
    if (callbacks == nullptr)
    {
        return nullptr;
    }
    try
    {
        auto handle = std::make_unique<ManagerHandle>();
        handle->host = std::make_unique<ShimHost>(*callbacks, state);
        handle->manager = std::make_unique<CalculationManager::CalculatorManager>(handle->host.get(), handle->host.get());
        return handle.release();
    }
    catch (...)
    {
        return nullptr;
    }
}

CALCSHIM_EXPORT void CalcShim_Destroy(void* handle)
{
    delete AsHandle(handle);
}

CALCSHIM_EXPORT void CalcShim_FreeString(char16_t* str)
{
    delete[] str;
}

CALCSHIM_EXPORT void CalcShim_Reset(void* handle, int32_t clearMemory)
{
    try
    {
        AsHandle(handle)->manager->Reset(clearMemory != 0);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_SetStandardMode(void* handle)
{
    try
    {
        AsHandle(handle)->manager->SetStandardMode();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_SetScientificMode(void* handle)
{
    try
    {
        AsHandle(handle)->manager->SetScientificMode();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_SetProgrammerMode(void* handle)
{
    try
    {
        AsHandle(handle)->manager->SetProgrammerMode();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_SendCommand(void* handle, int32_t command)
{
    try
    {
        AsHandle(handle)->manager->SendCommand(static_cast<CalculationManager::Command>(command));
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_MemorizeNumber(void* handle)
{
    try
    {
        AsHandle(handle)->manager->MemorizeNumber();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_MemorizedNumberLoad(void* handle, uint32_t index)
{
    try
    {
        AsHandle(handle)->manager->MemorizedNumberLoad(index);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_MemorizedNumberAdd(void* handle, uint32_t index)
{
    try
    {
        AsHandle(handle)->manager->MemorizedNumberAdd(index);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_MemorizedNumberSubtract(void* handle, uint32_t index)
{
    try
    {
        AsHandle(handle)->manager->MemorizedNumberSubtract(index);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_MemorizedNumberClear(void* handle, uint32_t index)
{
    try
    {
        AsHandle(handle)->manager->MemorizedNumberClear(index);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_MemorizedNumberClearAll(void* handle)
{
    try
    {
        AsHandle(handle)->manager->MemorizedNumberClearAll();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT int32_t CalcShim_IsEngineRecording(void* handle)
{
    try
    {
        return AsHandle(handle)->manager->IsEngineRecording() ? 1 : 0;
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT int32_t CalcShim_IsInputEmpty(void* handle)
{
    try
    {
        return AsHandle(handle)->manager->IsInputEmpty() ? 1 : 0;
    }
    catch (...)
    {
        return 1;
    }
}

CALCSHIM_EXPORT void CalcShim_SetRadix(void* handle, int32_t radixType)
{
    try
    {
        AsHandle(handle)->manager->SetRadix(static_cast<RadixType>(radixType));
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_SetMemorizedNumbersString(void* handle)
{
    try
    {
        AsHandle(handle)->manager->SetMemorizedNumbersString();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT char16_t* CalcShim_GetResultForRadix(void* handle, uint32_t radix, int32_t precision, int32_t groupDigitsPerRadix)
{
    try
    {
        std::wstring result = AsHandle(handle)->manager->GetResultForRadix(radix, precision, groupDigitsPerRadix != 0);
        return AllocUtf16(result);
    }
    catch (...)
    {
        return nullptr;
    }
}

CALCSHIM_EXPORT void CalcShim_SetPrecision(void* handle, int32_t precision)
{
    try
    {
        AsHandle(handle)->manager->SetPrecision(precision);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void CalcShim_UpdateMaxIntDigits(void* handle)
{
    try
    {
        AsHandle(handle)->manager->UpdateMaxIntDigits();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT char16_t CalcShim_DecimalSeparator(void* handle)
{
    try
    {
        return static_cast<char16_t>(AsHandle(handle)->manager->DecimalSeparator());
    }
    catch (...)
    {
        return u'.';
    }
}

CALCSHIM_EXPORT int32_t CalcShim_GetHistoryItemCount(void* handle, int32_t mode)
{
    try
    {
        auto const& items = AsHandle(handle)->manager->GetHistoryItems(static_cast<CalculationManager::CalculatorMode>(mode));
        return static_cast<int32_t>(items.size());
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT int32_t CalcShim_GetHistoryItemAt(void* handle, int32_t mode, uint32_t index, char16_t** expression, char16_t** result)
{
    if (expression != nullptr)
    {
        *expression = nullptr;
    }
    if (result != nullptr)
    {
        *result = nullptr;
    }
    try
    {
        auto const& items = AsHandle(handle)->manager->GetHistoryItems(static_cast<CalculationManager::CalculatorMode>(mode));
        if (index >= items.size() || items[index] == nullptr)
        {
            return 0;
        }
        if (expression != nullptr)
        {
            *expression = AllocUtf16(items[index]->historyItemVector.expression);
        }
        if (result != nullptr)
        {
            *result = AllocUtf16(items[index]->historyItemVector.result);
        }
        return 1;
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT int32_t CalcShim_RemoveHistoryItem(void* handle, uint32_t index)
{
    try
    {
        return AsHandle(handle)->manager->RemoveHistoryItem(index) ? 1 : 0;
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT void CalcShim_ClearHistory(void* handle)
{
    try
    {
        AsHandle(handle)->manager->ClearHistory();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT uint64_t CalcShim_MaxHistorySize(void* handle)
{
    try
    {
        return AsHandle(handle)->manager->MaxHistorySize();
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT int32_t CalcShim_GetCurrentDegreeMode(void* handle)
{
    try
    {
        return static_cast<int32_t>(AsHandle(handle)->manager->GetCurrentDegreeMode());
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT void CalcShim_SetInHistoryItemLoadMode(void* handle, int32_t isHistoryItemLoadMode)
{
    try
    {
        AsHandle(handle)->manager->SetInHistoryItemLoadMode(isHistoryItemLoadMode != 0);
    }
    catch (...)
    {
    }
}
