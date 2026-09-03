using System.Collections.Generic;
using Pulsar.Features.Presets.Models;

namespace Pulsar.Features.Presets.Services
{
    public interface IPresetCatalogService
    {
        IReadOnlyList<PresetPack> All { get; }

        PresetPack? GetById(string id);
    }
}
