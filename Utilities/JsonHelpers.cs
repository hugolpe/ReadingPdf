using System;
using System.Text.Json;

namespace ReadingPdf.Utilities
{
    // Small helper to safely serialize values that might be `dynamic` at runtime.
    // Use `JsonHelpers.SerializeDynamic(value, options)` instead of calling
    // `JsonSerializer.Serialize(value)` when `value` can be a `dynamic`/late-bound object.
    public static class JsonHelpers
    {
        public static string SerializeDynamic(object? value, JsonSerializerOptions? options = null)
        {
            // Null is a common case; delegate to the generic overload for clarity.
            if (value is null)
                return JsonSerializer.Serialize<object?>(null, options);

            // When the runtime value is dynamic, the generic overload JsonSerializer.Serialize<T>
            // can't be bound by the DLR. Call the non-generic overload specifying the runtime Type.
            return JsonSerializer.Serialize(value, value.GetType(), options);
        }
    }
}