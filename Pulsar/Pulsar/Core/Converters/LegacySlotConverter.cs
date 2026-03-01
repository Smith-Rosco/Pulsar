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
    /// Also auto-assigns Order field if missing (for backward compatibility)
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
                            // [Migration] Use slotIndex as Order if Order is not set
                            if (slot.Order == 0) slot.Order = slotIndex;
                            list.Add(slot);
                        }
                    }
                }
            }

            // [Auto-Assign Order] If any slot has Order = 0, assign sequential order
            NormalizeOrder(list);

            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<PluginSlot> value, JsonSerializerOptions options)
        {
            // Always write as Array (New Format)
            JsonSerializer.Serialize(writer, value, options);
        }

        /// <summary>
        /// 规范化 Order 字段：如果有任何 Slot 的 Order = 0，则按当前顺序自动分配 1, 2, 3...
        /// </summary>
        private static void NormalizeOrder(List<PluginSlot> slots)
        {
            if (slots.Count == 0) return;

            // 检查是否有未设置 Order 的 Slot
            bool hasUnsetOrder = slots.Any(s => s.Order == 0);
            
            if (hasUnsetOrder)
            {
                // 按当前顺序分配 Order
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Order == 0)
                    {
                        slots[i].Order = i + 1;
                    }
                }
            }

            // 按 Order 排序
            slots.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
}
