using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    /// <summary>
    /// Validates script prerequisites against active workbook.
    /// Supports checking for sheet existence, cell values, and range data.
    /// </summary>
    public class PrerequisiteValidator
    {
        private readonly ILogger? _logger;
        
        public PrerequisiteValidator(ILogger? logger = null)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Check if all prerequisites are met
        /// </summary>
        /// <param name="workbook">Active Excel/WPS workbook</param>
        /// <param name="requirements">List of requirement strings</param>
        /// <returns>Validation result with details</returns>
        public PrerequisiteResult Validate(dynamic workbook, List<string> requirements)
        {
            if (requirements.Count == 0)
                return PrerequisiteResult.Success();
            
            var missingItems = new List<string>();
            
            foreach (var requirement in requirements)
            {
                if (!ValidateSingleRequirement(workbook, requirement, out string? missing))
                {
                    missingItems.Add(missing ?? requirement);
                }
            }
            
            return missingItems.Count == 0 
                ? PrerequisiteResult.Success() 
                : PrerequisiteResult.Failure(missingItems);
        }
        
        private bool ValidateSingleRequirement(
            dynamic workbook, 
            string requirement, 
            out string? missingItem)
        {
            missingItem = null;
            
            try
            {
                // Parse requirement format: "Type=Value"
                var parts = requirement.Split('=', 2);
                if (parts.Length != 2)
                {
                    _logger?.LogWarning(
                        "[PrerequisiteValidator] Invalid requirement format: {Req}", 
                        requirement);
                    return true; // Skip invalid format (fail-open)
                }
                
                string type = parts[0].Trim().ToLowerInvariant();
                string value = parts[1].Trim().Trim('"');
                
                switch (type)
                {
                    case "sheet":
                        return ValidateSheetExists(workbook, value, out missingItem);
                    
                    case "cell":
                        return ValidateCellNotEmpty(workbook, value, out missingItem);
                    
                    case "range":
                        return ValidateRangeNotEmpty(workbook, value, out missingItem);
                    
                    default:
                        _logger?.LogWarning(
                            "[PrerequisiteValidator] Unknown requirement type: {Type}", 
                            type);
                        return true; // Unknown types pass by default (fail-open)
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, 
                    "[PrerequisiteValidator] Error validating requirement: {Req}", 
                    requirement);
                return true; // Errors pass by default (fail-open for robustness)
            }
        }
        
        private bool ValidateSheetExists(
            dynamic workbook, 
            string sheetName, 
            out string? missingItem)
        {
            missingItem = null;
            
            try
            {
                foreach (dynamic sheet in workbook.Worksheets)
                {
                    string currentName = sheet.Name;
                    if (currentName == sheetName)
                    {
                        _logger?.LogDebug(
                            "[PrerequisiteValidator] Sheet '{Sheet}' found", 
                            sheetName);
                        return true;
                    }
                }
                
                missingItem = $"Sheet '{sheetName}'";
                _logger?.LogDebug(
                    "[PrerequisiteValidator] Sheet '{Sheet}' not found", 
                    sheetName);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, 
                    "[PrerequisiteValidator] Error checking sheet existence");
                return true; // Fail-open on error
            }
        }
        
        private bool ValidateCellNotEmpty(
            dynamic workbook, 
            string cellAddress, 
            out string? missingItem)
        {
            missingItem = null;
            
            try
            {
                var activeSheet = workbook.ActiveSheet;
                var cell = activeSheet.Range[cellAddress];
                var value = cell.Value;
                
                if (value == null || string.IsNullOrWhiteSpace(value?.ToString()))
                {
                    missingItem = $"Cell '{cellAddress}' (empty)";
                    _logger?.LogDebug(
                        "[PrerequisiteValidator] Cell '{Cell}' is empty", 
                        cellAddress);
                    return false;
                }
                
                _logger?.LogDebug(
                    "[PrerequisiteValidator] Cell '{Cell}' has value", 
                    cellAddress);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, 
                    "[PrerequisiteValidator] Error checking cell value");
                return true; // Fail-open
            }
        }
        
        private bool ValidateRangeNotEmpty(
            dynamic workbook, 
            string rangeAddress, 
            out string? missingItem)
        {
            missingItem = null;
            
            try
            {
                var activeSheet = workbook.ActiveSheet;
                var range = activeSheet.Range[rangeAddress];
                
                // Check if any cell in range has value
                foreach (dynamic cell in range.Cells)
                {
                    var value = cell.Value;
                    if (value != null && !string.IsNullOrWhiteSpace(value?.ToString()))
                    {
                        _logger?.LogDebug(
                            "[PrerequisiteValidator] Range '{Range}' has data", 
                            rangeAddress);
                        return true;
                    }
                }
                
                missingItem = $"Range '{rangeAddress}' (all empty)";
                _logger?.LogDebug(
                    "[PrerequisiteValidator] Range '{Range}' is empty", 
                    rangeAddress);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, 
                    "[PrerequisiteValidator] Error checking range values");
                return true; // Fail-open
            }
        }
    }
    
    /// <summary>
    /// Result of prerequisite validation
    /// </summary>
    public class PrerequisiteResult
    {
        public bool IsValid { get; set; }
        public List<string> MissingItems { get; set; } = new();
        
        public static PrerequisiteResult Success() => 
            new PrerequisiteResult { IsValid = true };
        
        public static PrerequisiteResult Failure(List<string> missing) => 
            new PrerequisiteResult 
            { 
                IsValid = false, 
                MissingItems = missing 
            };
        
        public string GetErrorMessage()
        {
            if (IsValid) return string.Empty;
            
            return "Prerequisites not met:\n" + 
                   string.Join("\n", MissingItems.Select(m => $"  - {m}"));
        }
    }
}
