using System;

namespace SmartInventory.Core.Attributes;

/// <summary>
/// Indicates that a property is indexed in the database and is safe for dynamic sorting.
/// Prevents unindexed large text columns from causing "File Sort" exhaustion attacks.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SortableAttribute : Attribute
{
}
