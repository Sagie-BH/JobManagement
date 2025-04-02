using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Data;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace JobManagement.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly AppDbContext _dbContext;

        public Repository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<T> GetByIdAsync(Guid id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>().ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().Where(predicate).ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate,
                                                   Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                                   string includeString = null,
                                                   bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();

            if (disableTracking)
                query = query.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(includeString))
                query = query.Include(includeString);

            if (predicate != null)
                query = query.Where(predicate);

            if (orderBy != null)
                return await orderBy(query).ToListAsync();

            return await query.ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate,
                                                   Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                                   List<Expression<Func<T, object>>> includes = null,
                                                   bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();

            if (disableTracking)
                query = query.AsNoTracking();

            if (includes != null)
                query = includes.Aggregate(query, (current, include) => current.Include(include));

            if (predicate != null)
                query = query.Where(predicate);

            if (orderBy != null)
                return await orderBy(query).ToListAsync();

            return await query.ToListAsync();
        }

        public async Task<T> AddAsync(T entity)
        {
            // Set the created date if it's an auditable entity
            if (entity is AuditableEntity auditableEntity)
            {
                auditableEntity.CreatedOn = DateTime.UtcNow;
                auditableEntity.LastModifiedOn = DateTime.UtcNow;

                // Default creator info if not set
                if (string.IsNullOrEmpty(auditableEntity.CreatedBy))
                {
                    auditableEntity.CreatedBy = "System";
                }

                if (string.IsNullOrEmpty(auditableEntity.LastModifiedBy))
                {
                    auditableEntity.LastModifiedBy = auditableEntity.CreatedBy;
                }
            }

            await _dbContext.Set<T>().AddAsync(entity);
            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            // Update the modified date if it's an auditable entity
            if (entity is AuditableEntity auditableEntity)
            {
                auditableEntity.LastModifiedOn = DateTime.UtcNow;

                // Default modifier info if not set
                if (string.IsNullOrEmpty(auditableEntity.LastModifiedBy))
                {
                    auditableEntity.LastModifiedBy = "System";
                }
            }

            _dbContext.Entry(entity).State = EntityState.Modified;

            // If auditable, prevent changing the CreatedOn and CreatedBy fields
            if (entity is AuditableEntity)
            {
                _dbContext.Entry(entity).Property("CreatedOn").IsModified = false;
                _dbContext.Entry(entity).Property("CreatedBy").IsModified = false;
            }
        }

        public async Task DeleteAsync(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null)
        {
            if (predicate == null)
                return await _dbContext.Set<T>().CountAsync();
            return await _dbContext.Set<T>().Where(predicate).CountAsync();
        }
    }
}