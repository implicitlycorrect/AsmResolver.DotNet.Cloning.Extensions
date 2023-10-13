# AsmResolver.DotNet.Cloning.Extra
Attribute-Driven Cloning using AsmResolver.DotNet.Cloning

## Example usage
```csharp
// Load module from file
ModuleDefinition module = ModuleDefinition.FromFile(...);

// Create a new MemberCloner with the target module.
var cloner = new MemberCloner(module);

// Include members in the cloning process based on the presence of the [IncludeMember] attribute,
// while ignoring members marked with [IgnoreMember] attribute from the specified module
// (or the current process/module if 'module' is null).
cloner.IncludeAttributedMembers(module: null);

// Perform the cloning operation and add the cloned members to the target module.
cloner.Clone().AddToModule(module);

// Clone whole type and all its members (except for members with the [IgnoreMember] attribute)
[IncludeMember]
public static class StringEncryption
{
    private static readonly Dictionary<int, string> Cache = new();

    public static string Decrypt(string encrypted, int key)
    {
        // ...
    }

    [IgnoreMember]
    private static (int key, string encrypted) Encrypt(string text)
    {
        // ...
    }
}

// or for example use [IncludeMember] attribute on members to have them added to the <Module> type with the AddToModule method.
public static class StringEncryption
{
    [IncludeMember]
    private static readonly Dictionary<int, string> Cache = new();

    [IncludeMember]
    public static string Decrypt(string encrypted, int key)
    {
        // ...
    }
}
```

```csharp
// You can also use the [ModuleInitializer] attribute to have code executed in the static <Module> constuctor.
// (this will also be run by your application if you don't have any checks in place!)
// https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.moduleinitializerattribute?view=net-7.0
[IncludeMember]
[ModuleInitializer]
public static void MyMethod()
{
    string fullName = Assembly.GetExecutingAssembly().FullName;
    if (fullName == null || !fullName.Contains("freddyfazzbear"))
    {
        // ...
    }
}
```
