using Microsoft.EntityFrameworkCore;
using Heartbeat.Domain;

namespace Heartbeat.Server.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; set; }
}