using System;

namespace SmartInventory.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SortableAttribute : Attribute
{
}


// runtime checked for [sortable]
//prevents large text columsn and hels to assingn a column as sortable 