using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Plugins.Core.Pki.Models;

namespace Pulsar.Plugins.Core.Pki.Contracts
{
    public interface IPkiSecretStore
    {
        Task<Dictionary<Guid, SecretPayload>> LoadAsync();
        Task SaveAsync(Dictionary<Guid, SecretPayload> secrets);
    }
}
