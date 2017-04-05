﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AgEitilt.CardCatalog.Audio.Strings {
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
    public class ID3v24 {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal ID3v24() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("AgEitilt.CardCatalog.Audio.Strings.ID3v24", typeof(ID3v24).GetTypeInfo().Assembly);
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
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid length ({0}) given for ID3v2.4 &apos;CRC data present&apos; data (must be 5).
        /// </summary>
        public static string Exception_HeaderCrcTooShort {
            get {
                return ResourceManager.GetString("Exception_HeaderCrcTooShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid length ({0}) given for ID3v2.4 &apos;Tag restrictions&apos; data (must be 1).
        /// </summary>
        public static string Exception_HeaderRestrictionsTooShort {
            get {
                return ResourceManager.GetString("Exception_HeaderRestrictionsTooShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Extended header too short to be valid for ID3v2.4.
        /// </summary>
        public static string Exception_HeaderTooShort {
            get {
                return ResourceManager.GetString("Exception_HeaderTooShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid length ({0}) given for ID3v2.4 &apos;Tag is an update&apos; data (must be 0).
        /// </summary>
        public static string Exception_HeaderUpdateTooShort {
            get {
                return ResourceManager.GetString("Exception_HeaderUpdateTooShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Other credits.
        /// </summary>
        public static string Field_DefaultName_Credits {
            get {
                return ResourceManager.GetString("Field_DefaultName_Credits", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Encoding date.
        /// </summary>
        public static string Field_TDEN {
            get {
                return ResourceManager.GetString("Field_TDEN", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Original release date.
        /// </summary>
        public static string Field_TDOR {
            get {
                return ResourceManager.GetString("Field_TDOR", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Recording date.
        /// </summary>
        public static string Field_TDRC {
            get {
                return ResourceManager.GetString("Field_TDRC", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Release date.
        /// </summary>
        public static string Field_TDRL {
            get {
                return ResourceManager.GetString("Field_TDRL", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Tagging date.
        /// </summary>
        public static string Field_TDTG {
            get {
                return ResourceManager.GetString("Field_TDTG", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Production credits.
        /// </summary>
        public static string Field_TIPL {
            get {
                return ResourceManager.GetString("Field_TIPL", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Artist credits.
        /// </summary>
        public static string Field_TMCL {
            get {
                return ResourceManager.GetString("Field_TMCL", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}: {1}.
        /// </summary>
        public static string Field_Value_Credits {
            get {
                return ResourceManager.GetString("Field_Value_Credits", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}.
        /// </summary>
        public static string Field_Value_Credits_EmptyRole {
            get {
                return ResourceManager.GetString("Field_Value_Credits_EmptyRole", resourceCulture);
            }
        }
    }
}
