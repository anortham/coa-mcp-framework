using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace COA.Mcp.Framework.TokenOptimization.Utilities;

/// <summary>
/// Efficient object size estimation without JSON serialization.
/// Provides approximate memory footprint for cache size management.
/// </summary>
public static class ObjectSizeEstimator
{
    private static readonly int PointerSize = IntPtr.Size;
    private static readonly int ObjectHeaderSize = PointerSize * 2; // Object header overhead
    private const int StringHeaderSize = 24; // String object overhead
    private const int ArrayHeaderSize = 24; // Array object overhead
    
    /// <summary>
    /// Estimates the approximate memory footprint of an object in bytes.
    /// Much faster than JSON serialization for cache size calculations.
    /// </summary>
    /// <param name="obj">Object to estimate size for</param>
    /// <returns>Estimated size in bytes</returns>
    public static long EstimateSize(object? obj)
    {
        if (obj == null) return 0;
        
        return EstimateSizeInternal(obj, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }
    
    private static long EstimateSizeInternal(object obj, HashSet<object> visited)
    {
        if (obj == null) return 0;
        
        // Prevent infinite recursion on circular references
        if (!visited.Add(obj)) return PointerSize; // Just count the reference
        
        var type = obj.GetType();
        long size = ObjectHeaderSize;
        
        // Handle primitive types and known types
        if (type.IsPrimitive)
        {
            return Marshal.SizeOf(type);
        }
        
        if (obj is string str)
        {
            return StringHeaderSize + (str.Length * 2); // UTF-16 encoding
        }
        
        if (obj is Array array)
        {
            size += ArrayHeaderSize;
            foreach (var element in array)
            {
                size += EstimateSizeInternal(element, visited);
            }
            return size;
        }
        
        if (obj is ICollection collection)
        {
            size += 32; // Collection overhead
            foreach (var item in collection)
            {
                size += EstimateSizeInternal(item, visited);
            }
            return size;
        }
        
        if (obj is IDictionary dictionary)
        {
            size += 48; // Dictionary overhead
            foreach (DictionaryEntry entry in dictionary)
            {
                size += EstimateSizeInternal(entry.Key, visited);
                size += EstimateSizeInternal(entry.Value, visited);
            }
            return size;
        }
        
        // Handle generic dictionaries
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            size += 48; // Dictionary overhead
            var enumerableMethod = type.GetMethod("GetEnumerator");
            if (enumerableMethod != null)
            {
                var enumerator = enumerableMethod.Invoke(obj, null);
                if (enumerator is IEnumerator en)
                {
                    while (en.MoveNext())
                    {
                        var current = en.Current;
                        if (current != null)
                        {
                            // Handle KeyValuePair
                            var kvpType = current.GetType();
                            var keyProp = kvpType.GetProperty("Key");
                            var valueProp = kvpType.GetProperty("Value");
                            
                            if (keyProp != null && valueProp != null)
                            {
                                size += EstimateSizeInternal(keyProp.GetValue(current), visited);
                                size += EstimateSizeInternal(valueProp.GetValue(current), visited);
                            }
                        }
                    }
                }
            }
            return size;
        }
        
        // Handle reference types by examining fields
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (field.FieldType.IsValueType)
            {
                size += GetValueTypeSize(field.FieldType);
            }
            else
            {
                var fieldValue = field.GetValue(obj);
                size += EstimateSizeInternal(fieldValue, visited);
            }
        }
        
        return size;
    }
    
    private static int GetValueTypeSize(Type type)
    {
        if (type.IsPrimitive)
        {
            return Marshal.SizeOf(type);
        }
        
        if (type.IsEnum)
        {
            return Marshal.SizeOf(Enum.GetUnderlyingType(type));
        }
        
        // For structs, estimate based on fields
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        int size = 0;
        foreach (var field in fields)
        {
            if (field.FieldType.IsValueType)
            {
                size += GetValueTypeSize(field.FieldType);
            }
            else
            {
                size += PointerSize; // Reference field
            }
        }
        
        return Math.Max(size, 1); // Minimum size of 1 byte
    }
}

/// <summary>
/// Reference equality comparer for HashSet to prevent infinite recursion
/// </summary>
internal class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();
    
    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
    
    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}