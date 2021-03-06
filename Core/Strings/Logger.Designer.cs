﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AgEitilt.CardCatalog.Strings {
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
    internal class Logger {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Logger() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("AgEitilt.CardCatalog.Strings.Logger", typeof(Logger).GetTypeInfo().Assembly);
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
        ///   Looks up a localized string similar to Parsing field &quot;{Name}&quot; according to generic logic.
        /// </summary>
        internal static string Field_Parse {
            get {
                return ResourceManager.GetString("Field_Parse", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parsing type {Type} from stream.
        /// </summary>
        internal static string GenericParse {
            get {
                return ResourceManager.GetString("GenericParse", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Calling Parse for object of length {Length}.
        /// </summary>
        internal static string GenericParse_Bound {
            get {
                return ResourceManager.GetString("GenericParse_Bound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Calling Parse for object of unknown length.
        /// </summary>
        internal static string GenericParse_Bound_Unknown {
            get {
                return ResourceManager.GetString("GenericParse_Bound_Unknown", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Found header matching type {Type}.
        /// </summary>
        internal static string GenericParse_Found {
            get {
                return ResourceManager.GetString("GenericParse_Found", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reading {Count} additional bytes into header; total {Length}.
        /// </summary>
        internal static string GenericParse_Header {
            get {
                return ResourceManager.GetString("GenericParse_Header", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tag not able to be read as current type.
        /// </summary>
        internal static string GenericParse_Skip {
            get {
                return ResourceManager.GetString("GenericParse_Skip", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering all formats in assembly {Assembly}.
        /// </summary>
        internal static string RegisterAll {
            get {
                return ResourceManager.GetString("RegisterAll", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Formats in assembly have already been registered.
        /// </summary>
        internal static string RegisterAll_PrevAssembly {
            get {
                return ResourceManager.GetString("RegisterAll_PrevAssembly", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Checking type {Type} for registration.
        /// </summary>
        internal static string RegisterAll_TypeList {
            get {
                return ResourceManager.GetString("RegisterAll_TypeList", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering type {Type} as a field within {Format}.
        /// </summary>
        internal static string RegisterField {
            get {
                return ResourceManager.GetString("RegisterField", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Scanning for format attribute(s) on enclosing types.
        /// </summary>
        internal static string RegisterField_NoFormat {
            get {
                return ResourceManager.GetString("RegisterField_NoFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Scanning for additional field headers.
        /// </summary>
        internal static string RegisterField_ScanHeaders {
            get {
                return ResourceManager.GetString("RegisterField_ScanHeaders", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering {Method} as a generator for {Field} within {Format}.
        /// </summary>
        internal static string RegisterFieldParser {
            get {
                return ResourceManager.GetString("RegisterFieldParser", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering type {Type} as {Format}.
        /// </summary>
        internal static string RegisterFormat {
            get {
                return ResourceManager.GetString("RegisterFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Scanning for format attribute(s) on type {Type}.
        /// </summary>
        internal static string RegisterFormat_EmptyGeneric {
            get {
                return ResourceManager.GetString("RegisterFormat_EmptyGeneric", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering {Method} as a generator for {Format}.
        /// </summary>
        internal static string RegisterFormatParser {
            get {
                return ResourceManager.GetString("RegisterFormatParser", resourceCulture);
            }
        }
    }
}
