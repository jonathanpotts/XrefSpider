using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace XrefSpider
{
    /// <summary>
    /// Type maps for value-to-reference type names and vice versa.
    /// </summary>
    public class TypeMaps
    {
        /// <summary>
        /// Reference-to-value type name map.
        /// </summary>
        public readonly static ReadOnlyDictionary<string, string> ValueTypeNames = new(new Dictionary<string, string>
        {
            { "System.Boolean", "bool" },
            { "System.Byte", "byte" },
            { "System.SByte", "sbyte" },
            { "System.Char", "char" },
            { "System.Decimal", "decimal" },
            { "System.Double", "double" },
            { "System.Single", "float" },
            { "System.Int32", "int32" },
            { "System.UInt32", "uint32" },
            { "System.Int64", "long" },
            { "System.UInt64", "ulong" },
            { "System.Int16", "short" },
            { "System.UInt16", "ushort" },
            { "System.Object", "object" },
            { "System.String", "string" }
        });

        /// <summary>
        /// Value-to-reference type name map.
        /// </summary>
        public readonly static ReadOnlyDictionary<string, string> ReferenceTypeNames =
            new(ValueTypeNames.Append(new("dynamic", "System.Object"))
                .ToDictionary(x => x.Value, x => x.Key)
                );
    }
}
