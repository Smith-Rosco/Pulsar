using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulsar.Models;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// Handles migration from Dictionary-based slots ("Slot_1": {...}) to List-based slots ([{ "Slot": 1, ... }])
    /// Also migrates deprecated Order field to Slot field (for backward compatibility)
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
                            // [Migration] Use slotIndex as Slot if Slot is not set
                            if (slot.Slot == 0) slot.Slot = slotIndex;
                            list.Add(slot);
                        }
                    }
                }
            }

            // [Data Migration] Migrate Order → Slot for backward compatibility
            MigrateOrderToSlot(list);

            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<PluginSlot> value, JsonSerializerOptions options)
        {
            // Always write as Array (New Format)
            JsonSerializer.Serialize(writer, value, options);
        }

        /// <summary>
        /// 数据迁移：将废弃的 Order 字段迁移到 Slot 字段
        /// 如果 Slot = 0 但 Order > 0，则使用 Order 的值作为 Slot
        /// 如果两者都为 0，则按当前顺序自动分配 1, 2, 3...
        /// </summary>
        private static void MigrateOrderToSlot(List<PluginSlot> slots)
        {
            if (slots.Count == 0) return;

            // Step 1: Migrate Order → Slot
            foreach (var slot in slots)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                if (slot.Slot == 0 && slot.Order > 0)
                {
                    slot.Slot = slot.Order;
                }
#pragma warning restore CS0618
            }

            // Step 2: Auto-assign Slot for items with Slot = 0
            bool hasUnsetSlot = slots.Any(s => s.Slot == 0);
            
            if (hasUnsetSlot)
            {
                // 按当前顺序分配 Slot
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Slot == 0)
                    {
                        slots[i].Slot = i + 1;
                    }
                }
            }

            // Step 3: Sort by Slot
            slots.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        }
    }
}
