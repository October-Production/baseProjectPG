using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TreasuryPortal.DbCore.Log;
using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Repositories;
using TreasuryPortal.DbCore.Repositories.Abstract;
using TreasuryPortal.DbCore.UOW;
using TreasuryPortal.DbCore.UOW.Abstract;

namespace TreasuryPortal.DbCore;

public static class DbCoreServiceCollectionExtensions
{
    /// <summary>
    /// Весь слой данных одной строкой: настройки, подключение к PostgreSQL, системный журнал
    /// вместе с его фоновой записью и единая точка входа в репозитории.
    /// </summary>
    public static IServiceCollection AddDbCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DbCoreOptions>(configuration.GetSection(DbCoreOptions.SectionName));

        // Один источник подключений на приложение
        services.TryAddSingleton<PgDataSource>();

        // Журнал: очередь в памяти + одна фоновая служба, которая её пишет
        services.TryAddSingleton<LogQueue>();
        services.TryAddSingleton<ILogSink>(provider => provider.GetRequiredService<LogQueue>());
        services.TryAddSingleton<ILogRepository, LogRepository>();
        services.TryAddSingleton<ISystemLog, SystemLog>();
        services.AddHostedService<SystemLogWriter>();

        // Репозитории
        services.TryAddSingleton<ITreasuryUow, TreasuryUow>();

        return services;
    }
}
