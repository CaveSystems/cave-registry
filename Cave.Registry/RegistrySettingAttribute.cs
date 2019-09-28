using System;
using Microsoft.Win32;

namespace Cave
{
    /// <summary>
    /// Attribute for registry settings options.
    /// </summary>
    public class RegistrySettingAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the used value kind.
        /// </summary>
        public RegistryValueKind ValueKind { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether obfuscate the data to make it a little
        /// harder to read.
        /// This can only be used with <see cref="ValueKind"/> == <see cref="RegistryValueKind.Binary"/>.
        /// </summary>
        public bool Obfuscate { get; set; }
    }
}
