﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AgEitilt.CardCatalog.Audio.Strings.ID3v2 {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Exceptions {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Exceptions() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("AgEitilt.CardCatalog.Audio.Strings.ID3v2.Exceptions", typeof(Exceptions).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempted to access the value of an ID3v2 header from a FieldBase class.
        /// </summary>
        internal static string FieldBaseHeaderFlag {
            get {
                return ResourceManager.GetString("FieldBaseHeaderFlag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempted to reverse ID3v2 unsynchronization on a stream with an invalid byte following 0xFF.
        /// </summary>
        internal static string NotUnsynchronized {
            get {
                return ResourceManager.GetString("NotUnsynchronized", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempting to read a larger integer from ID3 stream than supported by the storage type.
        /// </summary>
        internal static string ParsedIntTooLarge {
            get {
                return ResourceManager.GetString("ParsedIntTooLarge", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reached end of ID3v2 stream while trying to read {0} unsynchronized bytes.
        /// </summary>
        internal static string StreamEnded {
            get {
                return ResourceManager.GetString("StreamEnded", resourceCulture);
            }
        }
    }
}
