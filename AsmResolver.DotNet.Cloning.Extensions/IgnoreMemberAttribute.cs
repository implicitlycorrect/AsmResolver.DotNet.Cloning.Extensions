namespace AsmResolver.DotNet.Cloning.Extensions;

/// <summary>
///     Indicates that a member should not be included when cloning types based on the presence of this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field |
                AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property |
                AttributeTargets.Struct)]
public sealed class IgnoreMemberAttribute : Attribute
{
}
