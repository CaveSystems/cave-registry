using System;
using Microsoft.Win32;

namespace Cave
{
    /// <summary>
    /// Provides automatic loading / saving of properties.
    /// </summary>
    /// <typeparam name="T">Instance type supported by save and load functions.</typeparam>
    public class RegistrySettings<T> : RegistrySettings
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrySettings{T}"/> class.
        /// </summary>
        /// <param name="regKey">Registry key to write to.</param>
        /// <param name="attributeTypes">If set this attribute is used to select properties.</param>
        /// <param name="inverse">Invert the selection of properties. Use this in conjunction with <see cref="System.Xml.Serialization.XmlAttributes.XmlIgnore"/>.</param>
        public RegistrySettings(RegistryKey regKey, bool inverse = false, params Type[] attributeTypes)
            : base(typeof(T), regKey, inverse, attributeTypes)
        {
        }

        /// <summary>
        /// Loads all public properties from the registry.
        /// </summary>
        /// <param name="instance">The instance to load.</param>
        public void Load(T instance) => base.Load(instance);

        /// <summary>
        /// Loads the specified public properties from the registry.
        /// </summary>
        /// <param name="instance">The instance to load.</param>
        /// <param name="propertyNames">Name of the properties to load.</param>
        public void Load(T instance, params string[] propertyNames) => base.Load(instance, propertyNames);

        /// <summary>
        /// Saves all public properties to the registry.
        /// </summary>
        /// <param name="instance">The instance to save.</param>
        public void Save(T instance) => base.Save(instance);

        /// <summary>
        /// Saves all specified public properties to the registry.
        /// </summary>
        /// <param name="instance">The instance to save.</param>
        /// <param name="propertyNames">Name of the properties to save.</param>
        public void Save(T instance, params string[] propertyNames) => base.Save(instance, propertyNames);
    }
}
