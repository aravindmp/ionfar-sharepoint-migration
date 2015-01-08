﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IonFar.SharePoint.Migration.Infrastructure;
using Microsoft.SharePoint.Client;

namespace IonFar.SharePoint.Migration
{
    public class Migrator : IMigrator
    {
        private readonly ClientContext _clientContext;
        public ILogger Logger { get; set; }

        public Migrator(ClientContext clientContext, ILogger logger)
        {
            _clientContext = clientContext;
            Logger = logger;
        }

        /// <summary>
        /// Migrates all IMigrations within the given assembly.
        /// </summary>
        /// <param name="assemblyContainingMigrations">The assembly containing the migrations to run.</param>
        /// <remarks>Migrations must be decorated with a <see cref="MigrationAttribute"/></remarks>
        public void Migrate(Assembly assemblyContainingMigrations)
        {
            Migrate(assemblyContainingMigrations, name => true);
        }

        /// <summary>
        /// Migrates all IMigrations within the given assembly.
        /// </summary>
        /// <param name="assemblyContainingMigrations">The assembly containing the migrations to run.</param>
        /// <param name="filter">A function that accepts a Type.FullName and returns true if the given type is to be migrated, otherwise false.</param>
        /// <remarks>Migrations must be decorated with a <see cref="MigrationAttribute"/></remarks>
        public void Migrate(Assembly assemblyContainingMigrations, Func<string, bool> filter)
        {
            try
            {
                LogInfo("Starting upgrade against SharePoint instance at " + _clientContext.Url);
                var availableMigrations = GetAvailableMigrations(assemblyContainingMigrations, filter);
                var appliedMigrations = GetAppliedMigrations();
                var migrationsToRun = GetMigrationsToRun(appliedMigrations, availableMigrations);

                if (!availableMigrations.Any())
                {
                    LogInfo("There are no migrations available in the specified assembly: " +
                            assemblyContainingMigrations.FullName);
                    return;
                }

                if (!migrationsToRun.Any())
                {
                    LogInfo("The SharePoint instance is up to date, there are no migrations to run.");
                    return;
                }

                foreach (var migrationInfo in migrationsToRun)
                {
                        try
                        {
                            LogInfo(string.Format("Upgrading to {0} by running {1}...",
                                migrationInfo.Version,
                                migrationInfo.FullName));

                            migrationInfo.ApplyMigration(_clientContext);
                            var properties = _clientContext.Site.RootWeb.AllProperties;
                            _clientContext.Load(properties);
                            properties.FieldValues.Add(migrationInfo.Id, migrationInfo);
                            
                            _clientContext.ExecuteQuery();
                            LogInfo("The migration is complete.");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex,
                                "The migration failed and the transaction has been rolled back. NOTE any changes to indexes are NON-TRANSACTIONAL and manual intervention may be required."
                            );

                            throw;
                        }
                    }
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "The migration process failed. NOTE any changes to indexes are NON-TRANSACTIONAL and manual intervention may be required."
                    );
                throw;
            }
        }

        private MigrationInfo[] GetMigrationsToRun(IEnumerable<MigrationInfo> appliedMigrations, IEnumerable<MigrationInfo> availableMigrations)
        {
            var appliedVersions = new HashSet<long>(appliedMigrations.Select(m => m.Version));

            return availableMigrations
                .Where(availableMigration => !appliedVersions.Contains(availableMigration.Version))
                .OrderBy(migrationToRun => migrationToRun.Version)
                .ToArray();
        }

        private MigrationInfo[] GetAppliedMigrations()
        {
            var properties = _clientContext.Site.RootWeb.AllProperties;
            _clientContext.Load(properties);
           var appliedMigrations = properties.FieldValues.Where(f => f.Key.StartsWith("")).Select(f => f.Value as MigrationInfo);

            return appliedMigrations.ToArray();
        }

        private MigrationInfo[] GetAvailableMigrations(Assembly assemblyContainingMigrations, Func<string, bool> filter)
        {
            var availableMigrations =
                assemblyContainingMigrations
                    .GetExportedTypes()
                    .Where(candidateType => typeof (IMigration).IsAssignableFrom(candidateType) &&
                        !candidateType.IsAbstract &&
                        candidateType.IsClass &&
                        filter(candidateType.FullName))
                    .Select(candidateType => new MigrationInfo(candidateType))
                    .ToArray();

            return availableMigrations;
        }

        private void LogInfo(string message)
        {
            if (Logger != null)
                Logger.Information(message);
        }
    }
}