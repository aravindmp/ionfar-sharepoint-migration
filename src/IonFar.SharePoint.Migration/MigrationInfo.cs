﻿using System;
using System.Reflection;
using Microsoft.SharePoint.Client;
using Newtonsoft.Json;

namespace IonFar.SharePoint.Migration
{
    /// <summary>
    /// The value stored in the Property Bag to track a <see cref="Migration"/> applied to SharePoint.
    /// </summary>
    public class MigrationInfo
    {
        public const string Prefix = "migrationinfos/";

        /// <summary>
        /// The type containing the Migration to be applied.
        /// </summary>
        public string MigrationType { get; protected set; }
        /// <summary>
        /// The Id property of this instance.
        /// </summary>
        public string Id { get; protected set; }
        /// <summary>
        /// Get the Version of <see cref="MigrationAttribute"/> associated with the MigrationType.
        /// </summary>
        public long Version { get; protected set; }
        /// <summary>
        /// Is this migration version needs to be always overriden
        /// </summary>
        public bool OverrideCurrentDeployment { get; protected set; }
        /// <summary>
        /// Gets the FullName of the MigrationType.
        /// </summary>
        public string FullName { get; protected set; }
        /// <summary>
        /// Gets the UTC DateTime the migration was applied.
        /// </summary>
        public DateTime AppliedAtUtc { get; protected set; }

        protected MigrationInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationInfo"/> class with a type containing a migration.
        /// </summary>
        /// <param name="migrationType">The type containing the migration.</param>
        public MigrationInfo(Type migrationType)
        {
            MigrationType = migrationType.AssemblyQualifiedName;
            FullName = migrationType.FullName;
            var migrationAttribute = migrationType.GetCustomAttribute<MigrationAttribute>(inherit: true);
            if (migrationAttribute != null)
            {
                Version = migrationAttribute.Version;
                OverrideCurrentDeployment = migrationAttribute.OverrideCurrentDeployment;
                Id = Prefix + Version;
            }

        }

        [JsonConstructor]
        private MigrationInfo(string migrationType, string id, long version, bool overrideCurrentDeployment, string fullName, DateTime appliedAtUtc)
        {
            MigrationType = migrationType;
            Id = id;
            Version = version;
            OverrideCurrentDeployment = overrideCurrentDeployment;
            FullName = fullName;
            AppliedAtUtc = appliedAtUtc;
        }

        public void ApplyMigration(ClientContext clientContext, ILogger logger)
        {
            var migrationtype = Type.GetType(MigrationType);
            var migration = (IMigration)Activator.CreateInstance(migrationtype);
            migration.Up(clientContext, logger);
            AppliedAtUtc = DateTime.UtcNow;
        }
    }
}