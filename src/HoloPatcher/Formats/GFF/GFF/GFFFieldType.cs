using System;
using System.Numerics;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.GFF
{

    /// <summary>
    /// The different types of fields based off what kind of data it stores.
    /// </summary>
    public enum GFFFieldType
    {
        UInt8 = 0,
        Int8 = 1,
        UInt16 = 2,
        Int16 = 3,
        UInt32 = 4,
        Int32 = 5,
        UInt64 = 6,
        Int64 = 7,
        Single = 8,
        Double = 9,
        String = 10,
        ResRef = 11,
        LocalizedString = 12,
        Binary = 13,
        Struct = 14,
        List = 15,
        Vector4 = 16,
        Vector3 = 17
    }

    public static class GFFFieldTypeExtensions
    {
        public static Type GetReturnType(this GFFFieldType fieldType)
        {
            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                case GFFFieldType.UInt16:
                case GFFFieldType.UInt32:
                case GFFFieldType.UInt64:
                case GFFFieldType.Int8:
                case GFFFieldType.Int16:
                case GFFFieldType.Int32:
                case GFFFieldType.Int64:
                    return typeof(int);
                case GFFFieldType.String:
                    return typeof(string);
                case GFFFieldType.ResRef:
                    return typeof(ResRef);
                case GFFFieldType.Vector3:
                    return typeof(Vector3);
                case GFFFieldType.Vector4:
                    return typeof(Vector4);
                case GFFFieldType.LocalizedString:
                    return typeof(LocalizedString);
                case GFFFieldType.Struct:
                    return typeof(GFFStruct);
                case GFFFieldType.List:
                    return typeof(GFFList);
                case GFFFieldType.Binary:
                    return typeof(byte[]);
                case GFFFieldType.Single:
                    return typeof(float);
                case GFFFieldType.Double:
                    return typeof(double);
                default:
                    return typeof(object);
            }
        }

        public static bool IsIntegerType(this GFFFieldType fieldType)
        {
            return fieldType == GFFFieldType.Int8 || fieldType == GFFFieldType.UInt8 ||
                   fieldType == GFFFieldType.Int16 || fieldType == GFFFieldType.UInt16 ||
                   fieldType == GFFFieldType.Int32 || fieldType == GFFFieldType.UInt32 ||
                   fieldType == GFFFieldType.Int64 || fieldType == GFFFieldType.UInt64;
        }

        public static bool IsFloatType(this GFFFieldType fieldType)
        {
            return fieldType == GFFFieldType.Single || fieldType == GFFFieldType.Double;
        }

        public static bool IsStringType(this GFFFieldType fieldType)
        {
            return fieldType == GFFFieldType.String || fieldType == GFFFieldType.ResRef;
        }
    }
}
