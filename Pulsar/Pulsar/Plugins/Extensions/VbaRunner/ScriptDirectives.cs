using System.Collections.Generic;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    /// <summary>
    /// Represents parsed directives from VBA script header.
    /// Directives control plugin behavior and script execution flow.
    /// </summary>
    public class ScriptDirectives
    {
        /// <summary>
        /// Runner directive - controls UI interaction mode.
        /// Values: "None", "ShowSheetSelector"
        /// Default: "None"
        /// </summary>
        public string Runner { get; set; } = "None";
        
        /// <summary>
        /// Macro entry point name.
        /// Default: "Main"
        /// </summary>
        public string Macro { get; set; } = "Main";
        
        /// <summary>
        /// Prerequisites that must be met before main execution.
        /// Format examples:
        /// - "Sheet=SheetName" - requires specific sheet to exist
        /// - "Cell=A1" - requires cell to have non-empty value
        /// - "Range=A1:B10" - requires at least one non-empty cell in range
        /// Multiple requirements can be specified with multiple @Requires directives.
        /// </summary>
        public List<string> Requires { get; set; } = new();
        
        /// <summary>
        /// Macro to call when prerequisites are missing.
        /// This macro should handle setup/initialization logic.
        /// Default: "Setup"
        /// </summary>
        public string OnMissing { get; set; } = "Setup";
        
        /// <summary>
        /// Whether to automatically select if only one valid option exists.
        /// Applies to ShowSheetSelector runner.
        /// Default: false
        /// </summary>
        public bool AutoSelectSingle { get; set; } = false;
        
        /// <summary>
        /// Filter pattern for sheet selector.
        /// Format: "exclude:pattern" or "include:pattern"
        /// Wildcards: * (any chars), ? (single char)
        /// Example: "exclude:_Config_*" hides sheets starting with "_Config_"
        /// </summary>
        public string? SheetFilter { get; set; }
    }
}
