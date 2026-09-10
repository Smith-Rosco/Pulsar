using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Plugins.Core.SecretFill.Models;

namespace Pulsar.Plugins.Core.SecretFill.Contracts
{
    public interface ISecretStore
    {
        Task<Dictionary<Guid, SecretPayload>> LoadAsync();
        Task SaveAsync(Dictionary<Guid, SecretPayload> secrets);
    }
}
