using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulsar.Models;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// Handles migration from Dictionary-based slots ("Slot_1": {...}) to List-based slots ([{ "Slot": 1, ... }])
    /// </summary>
    public class LegacySlotConverter : JsonConverter<List<PluginSlot>>
    {
        public override List<PluginSlot> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<PluginSlot>();

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                // New Format: Array of objects
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray) break;
                    
                    var slot = JsonSerializer.Deserialize<PluginSlot>(ref reader, options);
                    if (slot != null) list.Add(slot);
                }
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Legacy Format: Dictionary {"Slot_1": {...}, "Slot_2": {...}}
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) break;

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string key = reader.GetString() ?? "";
                        
                        // Parse slot number from key "Slot_X"
                        int slotIndex = 0;
                        if (key.StartsWith("Slot_") && int.TryParse(key.Substring(5), out int idx))
                        {
                            slotIndex = idx;
                        }

                        // Read value object
                        reader.Read(); // Move to StartObject
                        var slot = JsonSerializer.Deserialize<PluginSlot>(ref reader, options);
                        
                        if (slot != null)
                        {
                            // If Slot wasn't in the object, use the key
                            if (slot.Slot == 0) slot.Slot = slotIndex;
                            list.Add(slot);
                        }
                    }
                }
            }

            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<PluginSlot> value, JsonSerializerOptions options)
        {
            // Always write as Array (New Format)
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
