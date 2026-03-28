using Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Core.Repositories;

public interface IPublisherRepository
{
    Task<Publisher?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Publisher?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<Publisher?> FindByNameAsync(string userName, CancellationToken ct = default);
    Task CreateAsync(Publisher publisher, CancellationToken ct = default);
    Task UpdateAsync(Publisher publisher, CancellationToken ct = default);
    Task DeleteAsync(Publisher publisher, CancellationToken ct = default);
}
