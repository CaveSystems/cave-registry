using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace Cave
{
    /// <summary>
    /// Provides automatic loading / saving of properties.
    /// </summary>
    public class RegistrySettings
    {
        /// <summary>
        /// Gets the registry key.
        /// </summary>
        public RegistryKey RegKey { get; }

        /// <summary>
        /// Gets the type.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Provides property settings.
        /// </summary>
        class Property
        {
            public readonly string Name;
            public readonly RegistryValueKind ValueKind;
            public readonly bool Obfuscate;

            public readonly bool IsEnum;
            public readonly bool IsIConvertible;
            public readonly PropertyInfo PropertyInfo;
            public readonly MethodBase Method;
            public readonly Type PropertyType;

            internal Property(PropertyInfo propertyInfo)
            {
                var attribute = propertyInfo.GetAttribute<RegistrySettingAttribute>();
                ValueKind = attribute?.ValueKind ?? RegistryValueKind.String;
                Obfuscate = attribute?.Obfuscate ?? false;

                PropertyInfo = propertyInfo;
                PropertyType = propertyInfo.PropertyType;
                IsIConvertible = typeof(IConvertible).IsAssignableFrom(PropertyType);
                Name = PropertyInfo.Name;

#if NETSTANDARD13
                var typeInfo = PropertyType.GetTypeInfo();
                IsEnum = typeInfo.IsEnum;
                if (!IsIConvertible)
                {
                    Method = typeInfo.GetDeclaredMethod("Parse");
                }
#else
                IsEnum = PropertyType.IsEnum;
                if (!IsIConvertible)
                {
                    switch (ValueKind)
                    {
                        case RegistryValueKind.String:
                            Method = PropertyType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(string), }, null) ??
                                throw new Exception($"Type {propertyInfo.PropertyType} does neither implement IConvertible interface nor provide a static Parse(string) method!");
                            break;
                        case RegistryValueKind.Binary:
                            Method = PropertyType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(byte[]), }, null) ??
                                throw new Exception($"Type {propertyInfo.PropertyType} does neither implement IConvertible interface nor provide a static Parse(byte[]) method!");
                            break;
                        default:
                            throw new NotImplementedException($"ValueKind {ValueKind} not implemented!");
                    }
                }
#endif
            }

            public override string ToString() => PropertyInfo.ToString();

            internal void SaveValue(RegistryKey regKey, object instance)
            {
                switch (ValueKind)
                {
                    case RegistryValueKind.Binary:
                        SaveBinary(regKey, instance);
                        break;
                    case RegistryValueKind.String:
                        SaveString(regKey, instance);
                        break;
                    default:
                        throw new NotImplementedException($"ValueKind {ValueKind} not implemented!");
                }
            }

            void SaveBinary(RegistryKey regKey, object instance)
            {
                var value = PropertyInfo.GetValue(instance, null);
                byte[] data;
                if (IsEnum)
                {
                    data = new byte[8];
                    BitConverter.GetBytes(Convert.ToInt64(value)).CopyTo(data, 0);
                }
                else if (IsIConvertible)
                {
                    IConvertible convertible = value as IConvertible;
                    data = Encoding.UTF8.GetBytes(convertible?.ToString(CultureInfo.InvariantCulture));
                }
                else if (value is byte[])
                {
                    data = (byte[])value;
                }
                else
                {
                    data = Encoding.UTF8.GetBytes(value?.ToString() ?? string.Empty);
                }
                if (Obfuscate)
                {
                    data = data.Obfuscate();
                }
                regKey.SetValue(Name, data, ValueKind);
            }

            void SaveString(RegistryKey regKey, object instance)
            {
                object value;
                if (IsIConvertible)
                {
                    IConvertible convertible = PropertyInfo.GetValue(instance, null) as IConvertible;
                    value = convertible?.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // IsEnum or parsable
                    value = PropertyInfo.GetValue(instance, null);
                }
                var valueString = value == null ? string.Empty : $"\"{value}\"";
                regKey.SetValue(Name, valueString, ValueKind);
            }

            internal bool LoadValue(RegistryKey regKey, object instance)
            {
                switch (ValueKind)
                {
                    case RegistryValueKind.Binary:
                        return LoadBinary(regKey.GetValue(Name), instance);
                    case RegistryValueKind.String:
                        return LoadString(regKey.GetValue(Name), instance);
                    default:
                        throw new NotImplementedException($"ValueKind {ValueKind} not implemented!");
                }
            }

            bool LoadBinary(object regObject, object instance)
            {
                if (!(regObject is byte[] regData))
                {
                    return false;
                }

                if (Obfuscate)
                {
                    try
                    {
                        regData = regData.Deobfuscate();
                    }
                    catch
                    {
                        return false;
                    }
                }

                object value;
                if (IsEnum)
                {
                    value = Convert.ChangeType(BitConverter.ToInt64(regData, 0), PropertyType);
                }
                else if (PropertyType == typeof(byte[]))
                {
                    value = regData;
                }
                else if (IsIConvertible)
                {
                    value = Encoding.UTF8.GetString(regData);
                    value = Convert.ChangeType(value, PropertyType, CultureInfo.InvariantCulture);
                }
                else
                {
                    value = Method.Invoke(null, new object[] { regData });
                }
                PropertyInfo.SetValue(instance, value, null);
                return true;
            }

            bool LoadString(object regObject, object instance)
            {
                if (!(regObject is string regValue))
                {
                    return false;
                }

                if (regValue == string.Empty)
                {
                    regValue = null;
                }
                else if (regValue[0] == '"' && regValue[regValue.Length - 1] == '"')
                {
                    regValue = regValue.Substring(1, regValue.Length - 2);
                }

                object value;
                if (IsEnum)
                {
                    value = Enum.Parse(PropertyType, regValue);
                }
                else if (IsIConvertible)
                {
                    value = Convert.ChangeType(regValue, PropertyType, CultureInfo.InvariantCulture);
                }
                else
                {
                    value = Method.Invoke(null, new object[] { regValue });
                }
                PropertyInfo.SetValue(instance, value, null);
                return true;
            }
        }

        /// <summary>
        /// Gets a <see cref="IDictionary{TKey, TValue}"/> instance for all properties loaded/saved to the <see cref="RegKey"/>.
        /// </summary>
        IDictionary<string, Property> Properties { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrySettings"/> class.
        /// </summary>
        /// <param name="type">The type to write.</param>
        /// <param name="regKey">The key to write to.</param>
        /// <param name="attributeTypes">If set this attribute is used to select properties.</param>
        /// <param name="inverse">Invert the selection of properties. Use this in conjunction with <see cref="System.Xml.Serialization.XmlAttributes.XmlIgnore"/>.</param>
        public RegistrySettings(Type type, RegistryKey regKey, bool inverse = false, params Type[] attributeTypes)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            if (type.IsValueType)
            {
                throw new NotSupportedException($"ValueTypes are not supported. Type {Type} is a valuetype!");
            }

            RegKey = regKey ?? throw new ArgumentNullException(nameof(regKey));

            IEnumerable<PropertyInfo> propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (attributeTypes != null && attributeTypes.Length > 0)
            {
                propertyInfos = propertyInfos.Where(p => inverse ^ p.GetCustomAttributes(true).Any(a => attributeTypes.Contains(a.GetType())));
            }
            Properties = new Dictionary<string, Property>();
            foreach (var propertyInfo in propertyInfos)
            {
                if (propertyInfo.CanRead && propertyInfo.CanWrite)
                {
                    Property property = new Property(propertyInfo);
                    Properties.Add(property.Name, property);
                }
            }
        }

        /// <summary>
        /// Checks whether a specific property is present at the.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>True if the settings contains an element with the propertyName; otherwise, false. </returns>
        public bool Contains(string propertyName) => Properties.ContainsKey(propertyName);

        /// <summary>
        /// Loads all public properties from the registry.
        /// </summary>
        /// <param name="instance">The object instance to load.</param>
        public void Load(object instance)
        {
            if (instance?.GetType() != Type)
            {
                throw new ArgumentOutOfRangeException(nameof(instance), $"Instance with type {instance?.GetType()} does not match expected type {Type}!");
            }

            foreach (var property in Properties.Values)
            {
                property.LoadValue(RegKey, instance);
            }
        }

        /// <summary>
        /// Loads the specified public properties from the registry.
        /// </summary>
        /// <param name="instance">The object instance to load.</param>
        /// <param name="propertyNames">Name of the properties to load.</param>
        public void Load(object instance, params string[] propertyNames)
        {
            if (instance?.GetType() != Type)
            {
                throw new ArgumentOutOfRangeException(nameof(instance), $"Instance with type {instance?.GetType()} does not match expected type {Type}!");
            }

            foreach (var propertyName in propertyNames)
            {
                var property = Properties[propertyName];
                property.LoadValue(RegKey, instance);
            }
        }

        /// <summary>
        /// Saves all public properties to the registry.
        /// </summary>
        /// <param name="instance">The object instance to save.</param>
        public void Save(object instance)
        {
            if (instance?.GetType() != Type)
            {
                throw new ArgumentOutOfRangeException(nameof(instance), $"Instance with type {instance?.GetType()} does not match expected type {Type}!");
            }

            foreach (var property in Properties.Values)
            {
                property.SaveValue(RegKey, instance);
            }
        }

        /// <summary>
        /// Saves all specified public properties to the registry.
        /// </summary>
        /// <param name="instance">The object instance to save.</param>
        /// <param name="propertyNames">Name of the properties to save.</param>
        public void Save(object instance, params string[] propertyNames)
        {
            if (instance?.GetType() != Type)
            {
                throw new ArgumentOutOfRangeException(nameof(instance), $"Instance with type {instance?.GetType()} does not match expected type {Type}!");
            }

            foreach (var propertyName in propertyNames)
            {
                var property = Properties[propertyName];
                property.SaveValue(RegKey, instance);
            }
        }
    }
}
