#include <cstdint>
#include <memory>
#include <string>
#include <unordered_map>
#include <vector>

#include "ShimStrings.h"
#include "UnitConverter.h"
#include "Command.h"

#if defined(_WIN32)
#define CALCSHIM_EXPORT extern "C" __declspec(dllexport)
#else
#define CALCSHIM_EXPORT extern "C" __attribute__((visibility("default")))
#endif

using namespace UnitConversionManager;

namespace
{
    using shim::ToUtf16;
    using shim::FromUtf16;
    using shim::AllocUtf16;
}

extern "C"
{
    typedef struct UnitShimCallbacks
    {
        void (*display_callback)(void* state, const char16_t* from, const char16_t* to);
        void (*suggested_values_callback)(void* state, const char16_t* const* values, const int32_t* unit_ids, int32_t count);
        void (*max_digits_reached)(void* state);
    } UnitShimCallbacks;
}

namespace
{
    class ShimDataLoader final : public IConverterDataLoader
    {
    public:
        void LoadData() override
        {
        }

        std::vector<Category> GetOrderedCategories() override
        {
            return m_categories;
        }

        std::vector<Unit> GetOrderedUnits(const Category& c) override
        {
            auto it = m_categoryToUnits.find(c.id);
            return it != m_categoryToUnits.end() ? it->second : std::vector<Unit>{};
        }

        std::unordered_map<Unit, ConversionData, UnitHash> LoadOrderedRatios(const Unit& u) override
        {
            std::unordered_map<Unit, ConversionData, UnitHash> result;
            auto explicitIt = m_explicitConversions.find(u.id);
            if (explicitIt != m_explicitConversions.end())
            {
                for (const auto& [toId, data] : explicitIt->second)
                {
                    auto unitIt = m_idToUnit.find(toId);
                    if (unitIt != m_idToUnit.end())
                    {
                        result.emplace(unitIt->second, data);
                    }
                }
                result.emplace(u, ConversionData{ 1.0, 0.0, false });
                return result;
            }

            auto categoryIt = m_unitToCategory.find(u.id);
            auto factorIt = m_unitFactors.find(u.id);
            if (categoryIt == m_unitToCategory.end() || factorIt == m_unitFactors.end())
            {
                return result;
            }
            for (const Unit& v : m_categoryToUnits[categoryIt->second])
            {
                auto targetFactorIt = m_unitFactors.find(v.id);
                if (targetFactorIt != m_unitFactors.end() && targetFactorIt->second > 0)
                {
                    result.emplace(v, ConversionData{ factorIt->second / targetFactorIt->second, 0.0, false });
                }
            }
            return result;
        }

        bool SupportsCategory(const Category& target) override
        {
            return m_categoryToUnits.find(target.id) != m_categoryToUnits.end();
        }

        void AddCategory(int32_t id, std::wstring name, bool supportsNegative)
        {
            m_categories.emplace_back(id, std::move(name), supportsNegative);
            m_categoryToUnits.try_emplace(id);
        }

        void AddUnit(int32_t categoryId, const Unit& unit, double factor)
        {
            m_categoryToUnits[categoryId].push_back(unit);
            m_idToUnit[unit.id] = unit;
            m_unitToCategory[unit.id] = categoryId;
            m_unitFactors[unit.id] = factor;
        }

        void AddExplicitConversion(int32_t fromUnitId, int32_t toUnitId, const ConversionData& data)
        {
            m_explicitConversions[fromUnitId][toUnitId] = data;
        }

        const Category* FindCategory(int32_t id) const
        {
            for (const auto& category : m_categories)
            {
                if (category.id == id)
                {
                    return &category;
                }
            }
            return nullptr;
        }

        const Unit* FindUnit(int32_t id) const
        {
            auto it = m_idToUnit.find(id);
            return it != m_idToUnit.end() ? &it->second : nullptr;
        }

    private:
        std::vector<Category> m_categories;
        std::unordered_map<int, std::vector<Unit>> m_categoryToUnits;
        std::unordered_map<int, Unit> m_idToUnit;
        std::unordered_map<int, int> m_unitToCategory;
        std::unordered_map<int, double> m_unitFactors;
        std::unordered_map<int, std::unordered_map<int, ConversionData>> m_explicitConversions;
    };

    class ShimVMCallback final : public IUnitConverterVMCallback
    {
    public:
        ShimVMCallback(const UnitShimCallbacks& callbacks, void* state) : m_cb(callbacks), m_state(state)
        {
        }

        void DisplayCallback(const std::wstring& from, const std::wstring& to) override
        {
            if (m_cb.display_callback != nullptr)
            {
                std::u16string fromU16 = ToUtf16(from);
                std::u16string toU16 = ToUtf16(to);
                m_cb.display_callback(m_state, fromU16.c_str(), toU16.c_str());
            }
        }

        void SuggestedValueCallback(const std::vector<std::tuple<std::wstring, Unit>>& suggestedValues) override
        {
            if (m_cb.suggested_values_callback == nullptr)
            {
                return;
            }
            std::vector<std::u16string> storage;
            std::vector<const char16_t*> values;
            std::vector<int32_t> unitIds;
            storage.reserve(suggestedValues.size());
            values.reserve(suggestedValues.size());
            unitIds.reserve(suggestedValues.size());
            for (const auto& [value, unit] : suggestedValues)
            {
                storage.push_back(ToUtf16(value));
                unitIds.push_back(unit.id);
            }
            for (const auto& s : storage)
            {
                values.push_back(s.c_str());
            }
            m_cb.suggested_values_callback(m_state, values.data(), unitIds.data(), static_cast<int32_t>(values.size()));
        }

        void MaxDigitsReached() override
        {
            if (m_cb.max_digits_reached != nullptr)
            {
                m_cb.max_digits_reached(m_state);
            }
        }

    private:
        UnitShimCallbacks m_cb;
        void* m_state;
    };

    struct ConverterHandle
    {
        std::shared_ptr<ShimDataLoader> loader;
        std::shared_ptr<ShimVMCallback> vmCallback;
        std::shared_ptr<UnitConverter> converter;
    };

    ConverterHandle* AsHandle(void* handle)
    {
        return static_cast<ConverterHandle*>(handle);
    }
}

CALCSHIM_EXPORT void* UnitShim_CreateBuilder()
{
    try
    {
        return new ShimDataLoader();
    }
    catch (...)
    {
        return nullptr;
    }
}

CALCSHIM_EXPORT void UnitShim_DestroyBuilder(void* builder)
{
    delete static_cast<ShimDataLoader*>(builder);
}

CALCSHIM_EXPORT void UnitShim_AddCategory(void* builder, int32_t id, const char16_t* name, int32_t supportsNegative)
{
    try
    {
        static_cast<ShimDataLoader*>(builder)->AddCategory(id, FromUtf16(name), supportsNegative != 0);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void UnitShim_AddUnit(
    void* builder,
    int32_t categoryId,
    int32_t unitId,
    const char16_t* name,
    const char16_t* abbreviation,
    double factor,
    int32_t isConversionSource,
    int32_t isConversionTarget,
    int32_t isWhimsical)
{
    try
    {
        Unit unit(unitId, FromUtf16(name), FromUtf16(abbreviation), isConversionSource != 0, isConversionTarget != 0, isWhimsical != 0);
        static_cast<ShimDataLoader*>(builder)->AddUnit(categoryId, unit, factor);
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void UnitShim_AddExplicitConversion(
    void* builder,
    int32_t fromUnitId,
    int32_t toUnitId,
    double ratio,
    double offset,
    int32_t offsetFirst)
{
    try
    {
        static_cast<ShimDataLoader*>(builder)->AddExplicitConversion(fromUnitId, toUnitId, ConversionData{ ratio, offset, offsetFirst != 0 });
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void* UnitShim_Create(void* builder, const UnitShimCallbacks* callbacks, void* state)
{
    std::shared_ptr<ShimDataLoader> loader(static_cast<ShimDataLoader*>(builder));
    if (loader == nullptr || callbacks == nullptr)
    {
        return nullptr;
    }
    try
    {
        auto handle = std::make_unique<ConverterHandle>();
        handle->loader = loader;
        handle->vmCallback = std::make_shared<ShimVMCallback>(*callbacks, state);
        handle->converter = std::make_shared<UnitConverter>(handle->loader);
        handle->converter->SetViewModelCallback(handle->vmCallback);
        handle->converter->Initialize();

        std::vector<Category> categories = handle->loader->GetOrderedCategories();
        if (!categories.empty())
        {
            handle->converter->SetCurrentCategory(categories.front());
            std::vector<Unit> units = handle->loader->GetOrderedUnits(categories.front());
            if (!units.empty())
            {
                Unit fromUnit = units.front();
                Unit toUnit = units.front();
                for (const Unit& unit : units)
                {
                    if (unit.isConversionSource)
                    {
                        fromUnit = unit;
                        break;
                    }
                }
                for (const Unit& unit : units)
                {
                    if (unit.isConversionTarget)
                    {
                        toUnit = unit;
                        break;
                    }
                }
                handle->converter->SetCurrentUnitTypes(fromUnit, toUnit);
            }
        }

        return handle.release();
    }
    catch (...)
    {
        return nullptr;
    }
}

CALCSHIM_EXPORT void UnitShim_Destroy(void* handle)
{
    delete AsHandle(handle);
}

CALCSHIM_EXPORT int32_t UnitShim_SetCurrentCategory(void* handle, int32_t categoryId, int32_t* fromUnitId, int32_t* toUnitId)
{
    if (fromUnitId != nullptr)
    {
        *fromUnitId = -1;
    }
    if (toUnitId != nullptr)
    {
        *toUnitId = -1;
    }
    try
    {
        auto* h = AsHandle(handle);
        const Category* category = h->loader->FindCategory(categoryId);
        if (category == nullptr)
        {
            return 0;
        }
        auto [units, fromUnit, toUnit] = h->converter->SetCurrentCategory(*category);
        (void)units;
        if (fromUnitId != nullptr)
        {
            *fromUnitId = fromUnit.id;
        }
        if (toUnitId != nullptr)
        {
            *toUnitId = toUnit.id;
        }
        return 1;
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT int32_t UnitShim_GetCurrentCategory(void* handle)
{
    try
    {
        return AsHandle(handle)->converter->GetCurrentCategory().id;
    }
    catch (...)
    {
        return -1;
    }
}

CALCSHIM_EXPORT int32_t UnitShim_SetCurrentUnitTypes(void* handle, int32_t fromUnitId, int32_t toUnitId)
{
    try
    {
        auto* h = AsHandle(handle);
        const Unit* fromUnit = h->loader->FindUnit(fromUnitId);
        const Unit* toUnit = h->loader->FindUnit(toUnitId);
        if (fromUnit == nullptr || toUnit == nullptr)
        {
            return 0;
        }
        h->converter->SetCurrentUnitTypes(*fromUnit, *toUnit);
        return 1;
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT void UnitShim_SwitchActive(void* handle, const char16_t* newValue)
{
    try
    {
        AsHandle(handle)->converter->SwitchActive(FromUtf16(newValue));
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT int32_t UnitShim_IsSwitchedActive(void* handle)
{
    try
    {
        return AsHandle(handle)->converter->IsSwitchedActive() ? 1 : 0;
    }
    catch (...)
    {
        return 0;
    }
}

CALCSHIM_EXPORT void UnitShim_SendCommand(void* handle, int32_t command)
{
    try
    {
        AsHandle(handle)->converter->SendCommand(static_cast<Command>(command));
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void UnitShim_Calculate(void* handle)
{
    try
    {
        AsHandle(handle)->converter->Calculate();
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT char16_t* UnitShim_SaveUserPreferences(void* handle)
{
    try
    {
        return AllocUtf16(AsHandle(handle)->converter->SaveUserPreferences());
    }
    catch (...)
    {
        return nullptr;
    }
}

CALCSHIM_EXPORT void UnitShim_RestoreUserPreferences(void* handle, const char16_t* preferences)
{
    try
    {
        AsHandle(handle)->converter->RestoreUserPreferences(FromUtf16(preferences));
    }
    catch (...)
    {
    }
}

CALCSHIM_EXPORT void UnitShim_ResetCategoriesAndRatios(void* handle)
{
    try
    {
        AsHandle(handle)->converter->ResetCategoriesAndRatios();
    }
    catch (...)
    {
    }
}
