<!--
Copyright (c) DCSV. All rights reserved.
-->

# Copilot Code Review Instructions

## Language Version

This project uses **C# 14** with **.NET 10**.

### Extension Members (C# 14)

The `extension` keyword is valid C# 14 syntax. Do not suggest converting to static methods with `this` parameter:

```csharp
// Valid C# 14 - DO NOT flag as invalid
public static class MyExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMyService()
        {
            return services;
        }
    }
}
```
