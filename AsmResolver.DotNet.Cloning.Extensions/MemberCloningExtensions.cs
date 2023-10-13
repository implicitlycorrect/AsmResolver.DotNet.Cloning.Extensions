#if NET8
System.Runtime.CompilerServices;
#else
using System.Reflection;
#endif
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AsmResolver.DotNet.Cloning.Extensions;

/// <summary>
///     Provides extension methods for the <see cref="MemberCloner" /> class to clone members based on attributes.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Clones members of types in the specified module based on the presence of the [IncludeMemberAttribute].
    /// </summary>
    /// <param name="memberCloner">The <see cref="MemberCloner" /> instance to add cloned members to.</param>
    /// <param name="module">
    ///     The optional module definition to load types from. If not provided, the module
    ///     is determined automatically based on the current process.
    /// </param>
    /// <returns>The updated <see cref="MemberCloner" /> instance with cloned members added.</returns>
    public static MemberCloner IncludeAttributedMembers(this MemberCloner memberCloner,
        ModuleDefinition? module = null)
    {
        HashSet<TypeDefinition> typesToClone = GetTypesToClone(memberCloner);
        module ??= ModuleDefinition.FromModuleBaseAddress(Process.GetCurrentProcess().GetDotNetModuleBaseAddress());
        foreach (var type in module.GetAllTypes())
        {
            var includeType = type.HasCustomAttribute<IncludeMemberAttribute>();
            if (includeType)
                typesToClone.Add(type);

            foreach (var field in type.Fields)
            {
                var includeField = (includeType && !field.HasCustomAttribute<IgnoreMemberAttribute>()) ||
                                   field.HasCustomAttribute<IncludeMemberAttribute>();
                if (includeField)
                    memberCloner.Include(field);
            }

            foreach (var @event in type.Events)
            {
                var includeEvent = (includeType && !@event.HasCustomAttribute<IgnoreMemberAttribute>()) ||
                                   @event.HasCustomAttribute<IncludeMemberAttribute>();
                if (includeEvent)
                    memberCloner.Include(@event);
            }

            foreach (var property in type.Properties)
            {
                var includeProperty = (includeType && !property.HasCustomAttribute<IgnoreMemberAttribute>()) ||
                                      property.HasCustomAttribute<IncludeMemberAttribute>();
                if (includeProperty)
                    memberCloner.Include(property);
            }

            foreach (var method in type.Methods)
            {
                var includeMethod = !method.IsProperty() &&
                                    ((includeType && !method.HasCustomAttribute<IgnoreMemberAttribute>()) ||
                                     method.HasCustomAttribute<IncludeMemberAttribute>());
                if (includeMethod)
                    memberCloner.Include(method);
            }
        }

        return memberCloner;
    }

#if NET8
    /// <summary>
    ///     Gets the set of <see cref="TypeDefinition" /> objects representing the types to be cloned from the provided
    ///     <see cref="MemberCloner" />.
    /// </summary>
    /// <param name="memberCloner">The <see cref="MemberCloner" /> instance from which to retrieve the types to clone.</param>
    /// <returns>
    ///     A <see cref="HashSet{T}" /> of <see cref="TypeDefinition" /> objects representing the types to be cloned from the
    ///     <paramref name="memberCloner" />.
    /// </returns>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_typesToClone")]
    public static extern HashSet<TypeDefinition> GetTypesToClone(MemberCloner memberCloner);
#else
    /// <summary>
    ///     Gets the set of <see cref="TypeDefinition" /> objects representing the types to be cloned from the provided
    ///     <see cref="MemberCloner" />.
    /// </summary>
    /// <param name="memberCloner">The <see cref="MemberCloner" /> instance from which to retrieve the types to clone.</param>
    /// <returns>
    ///     A <see cref="HashSet{T}" /> of <see cref="TypeDefinition" /> objects representing the types to be cloned from the
    ///     <paramref name="memberCloner" />.
    /// </returns>
    private static HashSet<TypeDefinition> GetTypesToClone(MemberCloner memberCloner)
    {
        HashSet<TypeDefinition> typesToClone = (HashSet<TypeDefinition>?)typeof(MemberCloner)
                                                   .GetField("_typesToClone",
                                                       BindingFlags.Instance | BindingFlags.NonPublic)
                                                   ?.GetValue(memberCloner) ??
                                               throw new InvalidOperationException(
                                                   "Unable to retrieve the types to clone from the MemberCloner instance.");

        return typesToClone;
    }
#endif

    /// <summary>
    ///     Determines whether the provided method descriptor represents a property (getter or setter).
    /// </summary>
    /// <param name="methodDescriptor">The method descriptor to check.</param>
    /// <returns><c>true</c> if the method represents a property; otherwise, <c>false</c>.</returns>
    private static bool IsProperty(this IMethodDescriptor methodDescriptor)
    {
        return methodDescriptor.Name?.Value is { } methodName &&
               (methodName.StartsWith("get_") || methodName.StartsWith("set_"));
    }

    /// <summary>
    ///     Checks if the provided member has the [TAttribute].
    /// </summary>
    /// <param name="member">The member to check for the presence of the [TAttribute].</param>
    /// <returns><c>true</c> if the member has the [TAttribute]; otherwise, <c>false</c>.</returns>
    private static bool HasCustomAttribute<TAttribute>(this IHasCustomAttribute member) where TAttribute : Attribute
    {
        return member.HasCustomAttribute(typeof(TAttribute).Namespace, typeof(TAttribute).Name);
    }

    /// <summary>
    ///     Gets the base address of the .NET module associated with the specified process.
    /// </summary>
    /// <param name="process">The <see cref="Process" /> instance representing the running process.</param>
    /// <returns>The base address of the .NET module.</returns>
    private static IntPtr GetDotNetModuleBaseAddress(this Process process)
    {
#if NETFRAMEWORK
        return process.MainModule?.BaseAddress ?? throw new InvalidOperationException();
#else
        var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" : ".so";
        foreach (ProcessModule module in process.Modules)
            if (module.FileName.EndsWith(ext) && module.ModuleName.StartsWith(process.ProcessName) &&
                module.ModuleName.Length == process.ProcessName.Length + ext.Length)
                return module.BaseAddress;
        throw new InvalidOperationException();
#endif
    }

    /// <summary>
    ///     Adds cloned top-level types from the <paramref name="memberCloneResult" /> to the specified
    ///     <paramref name="targetModule" />.
    /// </summary>
    /// <param name="memberCloneResult">The result of cloning members using a <see cref="MemberCloner" />.</param>
    /// <param name="targetModule">The target <see cref="ModuleDefinition" /> where the cloned members should be added.</param>
    public static void AddToModule(this MemberCloneResult memberCloneResult, ModuleDefinition targetModule)
    {
        foreach (var typeDefinition in memberCloneResult.ClonedTopLevelTypes)
            targetModule.TopLevelTypes.Add(typeDefinition);

        var moduleType = targetModule.GetOrCreateModuleType();
        foreach (var clonedMember in memberCloneResult.ClonedMembers)
        {
            // Only members not declared in other types should be added to the <Module> type
            if (clonedMember.DeclaringType is not null)
                continue;

            switch (clonedMember.Resolve())
            {
                case FieldDefinition field:
                    moduleType.Fields.Add(field);
                    break;
                case EventDefinition @event:
                    moduleType.Events.Add(@event);
                    break;
                case PropertyDefinition property:
                    moduleType.Properties.Add(property);
                    break;
                case MethodDefinition method:
                    moduleType.Methods.Add(method);
                    break;
            }
        }
    }
}