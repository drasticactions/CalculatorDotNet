// Satisfies pch.h / UnitConverter.h on non-Windows toolchains, where MSVC's PPL
// is unavailable. UnitConverter only needs concurrency::task for its currency
// code paths (RefreshCurrencyRatios and ICurrencyConverterDataLoader), which the
// shim never exercises — no currency data loader is ever installed. A trivial
// synchronous task is therefore sufficient.
#pragma once

#include <utility>

namespace concurrency
{
    template <typename T>
    class task
    {
    public:
        task() = default;

        explicit task(T value) : m_value(std::move(value))
        {
        }

        T get() const
        {
            return m_value;
        }

        template <typename F>
        auto then(F&& func) const -> task<decltype(func(std::declval<T>()))>
        {
            using R = decltype(func(std::declval<T>()));
            return task<R>(func(m_value));
        }

    private:
        T m_value{};
    };

    template <typename T>
    task<T> task_from_result(T value)
    {
        return task<T>(std::move(value));
    }
}
